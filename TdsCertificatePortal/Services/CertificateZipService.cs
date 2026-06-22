using System.IO.Compression;
using System.Text.RegularExpressions;
using TdsCertificatePortal.Models;

namespace TdsCertificatePortal.Services;

public sealed class CertificateZipResult
{
    public List<PdfCertificate> Certificates { get; set; } = [];
    public List<PdfIssue> Issues { get; set; } = [];
    public int TotalPdfFiles { get; set; }
}

public sealed partial class CertificateZipService
{
    public async Task<CertificateZipResult> ExtractAsync(string zipPath, string outputFolder, CancellationToken cancellationToken)
    {
        var result = new CertificateZipResult();
        var pdfFolder = Path.Combine(outputFolder, "pdfs");
        Directory.CreateDirectory(pdfFolder);
        var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                if (!string.Equals(Path.GetExtension(entry.Name), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.TotalPdfFiles++;
                var safeFileName = Path.GetFileName(entry.Name);
                if (!seenFileNames.Add(safeFileName))
                {
                    result.Issues.Add(new PdfIssue
                    {
                        FileName = safeFileName,
                        Status = PortalStatuses.DuplicatePdf,
                        Detail = "Duplicate PDF filename found in ZIP."
                    });
                    continue;
                }

                var match = PdfFilenameRegex().Match(safeFileName);
                if (!match.Success)
                {
                    result.Issues.Add(new PdfIssue
                    {
                        FileName = safeFileName,
                        Status = PortalStatuses.InvalidFilename,
                        Detail = "Filename must include employee name, PAN number, AY code, and 16_PartB."
                    });
                    continue;
                }

                var storedPath = Path.Combine(pdfFolder, $"{Guid.NewGuid():N}_{safeFileName}");
                await using (var fileStream = File.Create(storedPath))
                await using (var entryStream = entry.Open())
                {
                    await entryStream.CopyToAsync(fileStream, cancellationToken);
                }

                var ayCode = match.Groups["ay"].Value;
                var namePart = match.Groups["name"].Value;
                result.Certificates.Add(new PdfCertificate
                {
                    FileName = safeFileName,
                    FilePath = storedPath,
                    EmployeeNameFromFile = TextNormalization.ToTitleFromFilenameName(namePart),
                    NormalizedEmployeeName = TextNormalization.NormalizeName(namePart),
                    PanNumber = TextNormalization.NormalizePan(match.Groups["pan"].Value),
                    AssessmentYearCode = ayCode,
                    AssessmentYearDisplay = TextNormalization.AssessmentYearDisplay(ayCode)
                });
            }
        }
        catch (InvalidDataException ex)
        {
            result.Issues.Add(new PdfIssue
            {
                FileName = Path.GetFileName(zipPath),
                Status = "Invalid ZIP File",
                Detail = ex.Message
            });
        }

        foreach (var duplicateGroup in result.Certificates.GroupBy(c => c.PanNumber).Where(g => g.Count() > 1))
        {
            foreach (var certificate in duplicateGroup)
            {
                certificate.IsDuplicatePdf = true;
                result.Issues.Add(new PdfIssue
                {
                    FileName = certificate.FileName,
                    Status = PortalStatuses.DuplicatePdf,
                    Detail = $"More than one PDF was found for PAN {certificate.PanNumber}.",
                    PanNumber = certificate.PanNumber,
                    EmployeeName = certificate.EmployeeNameFromFile
                });
            }
        }

        return result;
    }

    [GeneratedRegex("^(?<name>.+)_(?<pan>[A-Z]{5}\\d{4}[A-Z])_AY(?<ay>\\d{6})_16_PartB(?:_[A-Za-z0-9-]+)?\\.pdf$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PdfFilenameRegex();
}

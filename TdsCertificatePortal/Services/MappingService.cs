using TdsCertificatePortal.Models;

namespace TdsCertificatePortal.Services;

public sealed class MappingService
{
    public PortalSessionState BuildSessionState(
        SmtpSettings? smtpSettings,
        string tempFolder,
        IReadOnlyCollection<EmployeeInput> employees,
        IReadOnlyCollection<PdfCertificate> certificates,
        IReadOnlyCollection<PdfIssue> extractionIssues)
    {
        var state = new PortalSessionState
        {
            SmtpSettings = smtpSettings,
            TempFolderPath = tempFolder,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            PdfIssues = extractionIssues.ToList(),
            TotalPdfFiles = certificates.Count + extractionIssues.Count(i => i.Status == PortalStatuses.InvalidFilename)
        };

        var duplicateEmployeePans = employees
            .Where(e => !string.IsNullOrWhiteSpace(e.PanNumber))
            .GroupBy(e => e.PanNumber, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var certificatesByPan = certificates
            .Where(c => !c.IsDuplicatePdf)
            .GroupBy(c => c.PanNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var availableForNameMatch = certificates.Where(c => !c.IsDuplicatePdf).ToList();
        var assignedPdfPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var employee in employees)
        {
            var record = new EmployeeCertificateRecord
            {
                EmployeeCode = employee.EmployeeCode,
                EmployeeName = employee.EmployeeName,
                PanNumber = employee.PanNumber,
                EmailAddress = employee.EmailAddress
            };

            if (duplicateEmployeePans.Contains(employee.PanNumber))
            {
                record.Status = PortalStatuses.DuplicatePan;
                record.StatusDetail = "The uploaded employee CSV contains more than one row with this PAN.";
                state.Records.Add(record);
                continue;
            }

            PdfCertificate? matchedCertificate = null;
            if (certificatesByPan.TryGetValue(employee.PanNumber, out var panMatch))
            {
                matchedCertificate = panMatch;
                record.MatchedBy = "PAN";
            }
            else
            {
                matchedCertificate = availableForNameMatch.FirstOrDefault(c =>
                    !assignedPdfPaths.Contains(c.FilePath) &&
                    c.NormalizedEmployeeName == employee.NormalizedEmployeeName);
                if (matchedCertificate is not null)
                {
                    record.MatchedBy = "Employee Name";
                }
            }

            if (matchedCertificate is null)
            {
                record.Status = PortalStatuses.PdfMissing;
                record.StatusDetail = "No matching certificate PDF was found by PAN or employee name.";
                state.Records.Add(record);
                continue;
            }

            assignedPdfPaths.Add(matchedCertificate.FilePath);
            record.PdfFileName = matchedCertificate.FileName;
            record.PdfFilePath = matchedCertificate.FilePath;
            record.AssessmentYearDisplay = matchedCertificate.AssessmentYearDisplay;

            if (string.IsNullOrWhiteSpace(employee.EmailAddress))
            {
                record.Status = PortalStatuses.EmailMissing;
                record.StatusDetail = "Email address is required before sending.";
                record.DeliveryStatus = PortalStatuses.Skipped;
            }
            else
            {
                record.Status = PortalStatuses.ReadyToSend;
                record.StatusDetail = $"Matched by {record.MatchedBy}.";
                record.DeliveryStatus = PortalStatuses.Pending;
            }

            state.Records.Add(record);
        }

        var knownPans = employees.Select(e => e.PanNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownNames = employees.Select(e => e.NormalizedEmployeeName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var certificate in certificates.Where(c => !assignedPdfPaths.Contains(c.FilePath) && !c.IsDuplicatePdf))
        {
            var status = knownPans.Contains(certificate.PanNumber) ? PortalStatuses.EmployeeNotFound : PortalStatuses.PanNotFound;
            var detail = status == PortalStatuses.PanNotFound
                ? "PDF PAN was not found in the employee CSV."
                : "PDF PAN existed but could not be assigned to a unique employee record.";

            if (!knownNames.Contains(certificate.NormalizedEmployeeName) && status != PortalStatuses.PanNotFound)
            {
                status = PortalStatuses.EmployeeNotFound;
            }

            state.PdfIssues.Add(new PdfIssue
            {
                FileName = certificate.FileName,
                Status = status,
                Detail = detail,
                PanNumber = certificate.PanNumber,
                EmployeeName = certificate.EmployeeNameFromFile
            });
        }

        foreach (var certificate in certificates.Where(c => c.IsDuplicatePdf))
        {
            var employee = employees.FirstOrDefault(e => string.Equals(e.PanNumber, certificate.PanNumber, StringComparison.OrdinalIgnoreCase));
            if (employee is null)
            {
                continue;
            }

            var record = state.Records.FirstOrDefault(r => string.Equals(r.PanNumber, employee.PanNumber, StringComparison.OrdinalIgnoreCase));
            if (record is not null && string.IsNullOrWhiteSpace(record.PdfFileName))
            {
                record.Status = PortalStatuses.DuplicatePdf;
                record.StatusDetail = "Multiple PDF files were found for this employee PAN.";
                record.PdfFileName = certificate.FileName;
                record.PdfFilePath = certificate.FilePath;
                record.AssessmentYearDisplay = certificate.AssessmentYearDisplay;
            }
        }

        return state;
    }
}

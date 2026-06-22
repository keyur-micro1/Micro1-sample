using System.IO.Compression;
using System.Security;
using System.Text;
using TdsCertificatePortal.Models;

namespace TdsCertificatePortal.Services;

public sealed class ReportFile
{
    public byte[] Bytes { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public sealed class ReportService
{
    public ReportFile BuildMappingReport(IReadOnlyCollection<EmployeeCertificateRecord> records, string format)
    {
        var rows = records
            .Select(r => new[]
            {
                r.EmployeeCode,
                r.EmployeeName,
                r.PanNumber,
                r.EmailAddress,
                r.PdfFileName ?? string.Empty,
                r.Status,
                r.StatusDetail
            })
            .ToList();
        var headers = new[] { "Employee Code", "Employee Name", "PAN Number", "Email Address", "PDF Filename", "Mapping Status", "Details" };
        return BuildReport("mapping-report", headers, rows, format);
    }

    public ReportFile BuildDeliveryReport(IReadOnlyCollection<EmployeeCertificateRecord> records, string format)
    {
        var rows = records
            .Select(r => new[]
            {
                r.EmployeeCode,
                r.EmployeeName,
                r.PanNumber,
                r.EmailAddress,
                r.PdfFileName ?? string.Empty,
                r.SentAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                string.IsNullOrWhiteSpace(r.DeliveryStatus) ? PortalStatuses.Skipped : r.DeliveryStatus,
                r.ErrorMessage ?? string.Empty
            })
            .ToList();
        var headers = new[] { "Employee Code", "Employee Name", "PAN Number", "Email Address", "PDF Filename", "Sent Date & Time", "Delivery Status", "Error Message" };
        return BuildReport("email-delivery-report", headers, rows, format);
    }

    private static ReportFile BuildReport(string baseName, string[] headers, List<string[]> rows, string format)
    {
        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return new ReportFile
            {
                Bytes = BuildXlsx(headers, rows),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"{baseName}.xlsx"
            };
        }

        return new ReportFile
        {
            Bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BuildCsv(headers, rows))).ToArray(),
            ContentType = "text/csv",
            FileName = $"{baseName}.csv"
        };
    }

    private static string BuildCsv(string[] headers, IReadOnlyCollection<string[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static byte[] BuildXlsx(string[] headers, IReadOnlyCollection<string[]> rows)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            AddEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            AddEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Report" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);
            AddEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            AddEntry(archive, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <fonts count="2"><font/><font><b/></font></fonts>
                  <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
                  <borders count="1"><border/></borders>
                  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                  <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" applyFont="1"/></cellXfs>
                </styleSheet>
                """);
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheet(headers, rows));
        }

        return memory.ToArray();
    }

    private static string BuildWorksheet(string[] headers, IReadOnlyCollection<string[]> rows)
    {
        var builder = new StringBuilder();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        AppendRow(builder, 1, headers, isHeader: true);
        var rowIndex = 2;
        foreach (var row in rows)
        {
            AppendRow(builder, rowIndex++, row, isHeader: false);
        }

        builder.Append("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, int rowIndex, string[] values, bool isHeader)
    {
        builder.Append($"<row r=\"{rowIndex}\">");
        for (var index = 0; index < values.Length; index++)
        {
            var cellRef = $"{ColumnName(index + 1)}{rowIndex}";
            var style = isHeader ? " s=\"1\"" : string.Empty;
            builder.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"{style}><is><t>{SecurityElement.Escape(values[index])}</t></is></c>");
        }

        builder.Append("</row>");
    }

    private static string ColumnName(int number)
    {
        var dividend = number;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content.Trim());
    }
}

namespace TdsCertificatePortal.Models;

public sealed class PdfIssue
{
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? PanNumber { get; set; }
    public string? EmployeeName { get; set; }
}

namespace TdsCertificatePortal.Models;

public sealed class PdfCertificate
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string EmployeeNameFromFile { get; set; } = string.Empty;
    public string NormalizedEmployeeName { get; set; } = string.Empty;
    public string PanNumber { get; set; } = string.Empty;
    public string AssessmentYearCode { get; set; } = string.Empty;
    public string AssessmentYearDisplay { get; set; } = string.Empty;
    public bool IsDuplicatePdf { get; set; }
}

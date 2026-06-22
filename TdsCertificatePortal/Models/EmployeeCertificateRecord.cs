namespace TdsCertificatePortal.Models;

public sealed class EmployeeCertificateRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string PanNumber { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string? PdfFileName { get; set; }
    public string? PdfFilePath { get; set; }
    public string Status { get; set; } = PortalStatuses.Pending;
    public string StatusDetail { get; set; } = string.Empty;
    public string? MatchedBy { get; set; }
    public string? AssessmentYearDisplay { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public string DeliveryStatus { get; set; } = PortalStatuses.Skipped;
    public string? ErrorMessage { get; set; }

    public bool HasPdf => !string.IsNullOrWhiteSpace(PdfFilePath);
    public bool CanSend => PortalStatuses.CanSend(Status) && HasPdf && !string.IsNullOrWhiteSpace(EmailAddress);
}

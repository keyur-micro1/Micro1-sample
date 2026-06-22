namespace TdsCertificatePortal.Models;

public sealed class PortalSessionState
{
    public SmtpSettings? SmtpSettings { get; set; }
    public string? TempFolderPath { get; set; }
    public DateTimeOffset? UploadedAtUtc { get; set; }
    public List<EmployeeCertificateRecord> Records { get; set; } = [];
    public List<PdfIssue> PdfIssues { get; set; } = [];
    public int TotalPdfFiles { get; set; }
    public string SubjectTemplate { get; set; } = "TDS Certificate - AY {{AssessmentYear}}";
    public string BodyTemplate { get; set; } = "Dear {{EmployeeName}},\r\n\r\nPlease find attached your TDS Certificate (Form 16 Part B) for Assessment Year {{AssessmentYear}}.\r\n\r\nFor any queries, please contact the HR department.\r\n\r\nRegards,\r\nHR Team";

    public ProcessingSummary BuildSummary()
    {
        return new ProcessingSummary
        {
            TotalEmployees = Records.Count,
            TotalPdfs = TotalPdfFiles,
            MatchedRecords = Records.Count(r => r.HasPdf && r.Status is not PortalStatuses.DuplicatePdf),
            UnmatchedEmployees = Records.Count(r => r.Status is PortalStatuses.PdfMissing or PortalStatuses.DuplicatePan),
            UnmatchedPdfs = PdfIssues.Count(i => i.Status is PortalStatuses.PanNotFound or PortalStatuses.EmployeeNotFound or PortalStatuses.InvalidFilename or PortalStatuses.DuplicatePdf),
            MissingEmails = Records.Count(r => r.Status == PortalStatuses.EmailMissing),
            DuplicatePans = Records.Count(r => r.Status == PortalStatuses.DuplicatePan),
            DuplicatePdfs = PdfIssues.Count(i => i.Status == PortalStatuses.DuplicatePdf),
            ReadyToSend = Records.Count(r => r.Status == PortalStatuses.ReadyToSend),
            Sent = Records.Count(r => r.Status == PortalStatuses.Sent),
            Failed = Records.Count(r => r.Status == PortalStatuses.Failed)
        };
    }
}

namespace TdsCertificatePortal.Models.ViewModels;

public sealed class IndexViewModel
{
    public SmtpSettings SmtpSettings { get; set; } = new();
    public bool HasSmtpSettings { get; set; }
    public bool HasRecords { get; set; }
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public ProcessingSummary Summary { get; set; } = new();
    public List<EmployeeCertificateRecord> Records { get; set; } = [];
    public List<PdfIssue> PdfIssues { get; set; } = [];
    public string[] Statuses { get; set; } = PortalStatuses.Filterable;
}

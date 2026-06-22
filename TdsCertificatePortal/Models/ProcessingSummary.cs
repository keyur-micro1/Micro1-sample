namespace TdsCertificatePortal.Models;

public sealed class ProcessingSummary
{
    public int TotalEmployees { get; set; }
    public int TotalPdfs { get; set; }
    public int MatchedRecords { get; set; }
    public int UnmatchedEmployees { get; set; }
    public int UnmatchedPdfs { get; set; }
    public int MissingEmails { get; set; }
    public int DuplicatePans { get; set; }
    public int DuplicatePdfs { get; set; }
    public int ReadyToSend { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
}

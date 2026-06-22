namespace TdsCertificatePortal.Models;

public static class PortalStatuses
{
    public const string Matched = "Matched";
    public const string ReadyToSend = "Ready To Send";
    public const string PanNotFound = "PAN Not Found";
    public const string EmployeeNotFound = "Employee Not Found";
    public const string EmailMissing = "Email Missing";
    public const string DuplicatePan = "Duplicate PAN";
    public const string DuplicatePdf = "Duplicate PDF";
    public const string InvalidFilename = "Invalid Filename";
    public const string PdfMissing = "PDF Missing";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Skipped = "Skipped";

    public static readonly string[] Filterable =
    [
        Matched,
        ReadyToSend,
        PanNotFound,
        EmployeeNotFound,
        EmailMissing,
        DuplicatePan,
        DuplicatePdf,
        InvalidFilename,
        PdfMissing,
        Sent,
        Failed
    ];

    public static bool CanSend(string? status)
    {
        return status is ReadyToSend or Sent or Failed;
    }
}

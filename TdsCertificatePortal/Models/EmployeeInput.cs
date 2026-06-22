namespace TdsCertificatePortal.Models;

public sealed class EmployeeInput
{
    public int RowNumber { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string PanNumber { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string NormalizedEmployeeName { get; set; } = string.Empty;
}

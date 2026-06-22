using System.ComponentModel.DataAnnotations;

namespace TdsCertificatePortal.Models;

public sealed class SmtpSettings
{
    [Required, EmailAddress, Display(Name = "Sender Email Address")]
    public string SenderEmail { get; set; } = string.Empty;

    [Display(Name = "SMTP Username")]
    public string? UserName { get; set; }

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Display(Name = "SMTP Host")]
    public string SmtpHost { get; set; } = string.Empty;

    [Required, Range(1, 65535), Display(Name = "SMTP Port")]
    public int SmtpPort { get; set; } = 587;

    [Display(Name = "SSL/TLS Enabled")]
    public bool EnableSsl { get; set; } = true;

    [Required, EmailAddress, Display(Name = "CC Email Address")]
    public string CcEmail { get; set; } = string.Empty;

    [EmailAddress, Display(Name = "Test Recipient Email")]
    public string? TestRecipientEmail { get; set; }

    public string EffectiveUserName => string.IsNullOrWhiteSpace(UserName) ? SenderEmail : UserName.Trim();
}

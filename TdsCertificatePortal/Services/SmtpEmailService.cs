using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using TdsCertificatePortal.Models;

namespace TdsCertificatePortal.Services;

public sealed class SmtpOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static SmtpOperationResult Ok(string message) => new() { Success = true, Message = message };
    public static SmtpOperationResult Fail(string message) => new() { Success = false, Message = message };
}

public sealed class SmtpEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(ILogger<SmtpEmailService> logger)
    {
        _logger = logger;
    }

    public async Task<SmtpOperationResult> ValidateConnectionAsync(SmtpSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(settings.SmtpHost, settings.SmtpPort, cancellationToken);

            var networkStream = tcpClient.GetStream();
            Stream activeStream = networkStream;
            SslStream? sslStream = null;
            StreamReader reader;
            StreamWriter writer;

            async Task ResetReaderWriterAsync()
            {
                reader = new StreamReader(activeStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                writer = new StreamWriter(activeStream, Encoding.ASCII, bufferSize: 1024, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };
                await Task.CompletedTask;
            }

            reader = null!;
            writer = null!;

            if (settings.EnableSsl && settings.SmtpPort == 465)
            {
                sslStream = new SslStream(networkStream, true);
                await sslStream.AuthenticateAsClientAsync(settings.SmtpHost);
                activeStream = sslStream;
            }

            await ResetReaderWriterAsync();
            var welcome = await ReadReplyAsync(reader);
            if (!welcome.StartsWith("220", StringComparison.Ordinal))
            {
                return SmtpOperationResult.Fail($"SMTP server did not return a ready response: {welcome}");
            }

            await writer.WriteLineAsync("EHLO localhost");
            var ehlo = await ReadReplyAsync(reader);
            if (!ehlo.StartsWith("250", StringComparison.Ordinal))
            {
                return SmtpOperationResult.Fail($"SMTP EHLO failed: {ehlo}");
            }

            if (settings.EnableSsl && settings.SmtpPort != 465 && ehlo.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("STARTTLS");
                var startTls = await ReadReplyAsync(reader);
                if (!startTls.StartsWith("220", StringComparison.Ordinal))
                {
                    return SmtpOperationResult.Fail($"SMTP STARTTLS failed: {startTls}");
                }

                sslStream = new SslStream(networkStream, true);
                await sslStream.AuthenticateAsClientAsync(settings.SmtpHost);
                activeStream = sslStream;
                await ResetReaderWriterAsync();
                await writer.WriteLineAsync("EHLO localhost");
                ehlo = await ReadReplyAsync(reader);
                if (!ehlo.StartsWith("250", StringComparison.Ordinal))
                {
                    return SmtpOperationResult.Fail($"SMTP EHLO after STARTTLS failed: {ehlo}");
                }
            }

            await writer.WriteLineAsync("AUTH LOGIN");
            var authUserPrompt = await ReadReplyAsync(reader);
            if (!authUserPrompt.StartsWith("334", StringComparison.Ordinal))
            {
                return SmtpOperationResult.Fail($"SMTP AUTH LOGIN was not accepted: {authUserPrompt}");
            }

            await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.EffectiveUserName)));
            var authPasswordPrompt = await ReadReplyAsync(reader);
            if (!authPasswordPrompt.StartsWith("334", StringComparison.Ordinal))
            {
                return SmtpOperationResult.Fail($"SMTP username was rejected: {authPasswordPrompt}");
            }

            await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.Password)));
            var authResult = await ReadReplyAsync(reader);
            if (!authResult.StartsWith("235", StringComparison.Ordinal))
            {
                return SmtpOperationResult.Fail($"SMTP authentication failed: {authResult}");
            }

            await writer.WriteLineAsync("QUIT");
            sslStream?.Dispose();
            return SmtpOperationResult.Ok("SMTP connection and authentication succeeded.");
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "SMTP validation failed for host {SmtpHost}:{SmtpPort}", settings.SmtpHost, settings.SmtpPort);
            return SmtpOperationResult.Fail(ex.Message);
        }
    }

    public async Task<SmtpOperationResult> SendTestEmailAsync(SmtpSettings settings, CancellationToken cancellationToken)
    {
        var recipient = string.IsNullOrWhiteSpace(settings.TestRecipientEmail) ? settings.SenderEmail : settings.TestRecipientEmail.Trim();
        using var message = new MailMessage(settings.SenderEmail, recipient)
        {
            Subject = "SMTP Test - TDS Certificate Portal",
            Body = "SMTP configuration was validated by the TDS Certificate Email Distribution Portal.",
            IsBodyHtml = false
        };

        if (!string.IsNullOrWhiteSpace(settings.CcEmail))
        {
            message.CC.Add(settings.CcEmail);
        }

        return await SendMessageAsync(settings, message, cancellationToken);
    }

    public async Task<SmtpOperationResult> SendCertificateAsync(
        SmtpSettings settings,
        EmployeeCertificateRecord record,
        string subjectTemplate,
        string bodyTemplate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.EmailAddress))
        {
            return SmtpOperationResult.Fail("Employee email address is missing.");
        }

        if (string.IsNullOrWhiteSpace(record.PdfFilePath) || !File.Exists(record.PdfFilePath))
        {
            return SmtpOperationResult.Fail("Certificate PDF is missing from temporary storage.");
        }

        using var message = new MailMessage(settings.SenderEmail, record.EmailAddress.Trim())
        {
            Subject = RenderTemplate(subjectTemplate, record),
            Body = RenderTemplate(bodyTemplate, record),
            IsBodyHtml = false
        };

        if (!string.IsNullOrWhiteSpace(settings.CcEmail))
        {
            message.CC.Add(settings.CcEmail);
        }

        message.Attachments.Add(new Attachment(record.PdfFilePath)
        {
            Name = record.PdfFileName ?? Path.GetFileName(record.PdfFilePath)
        });

        return await SendMessageAsync(settings, message, cancellationToken);
    }

    private async Task<SmtpOperationResult> SendMessageAsync(SmtpSettings settings, MailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.EnableSsl,
                Credentials = new NetworkCredential(settings.EffectiveUserName, settings.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };
            await client.SendMailAsync(message, cancellationToken);
            return SmtpOperationResult.Ok("Email sent successfully.");
        }
        catch (SmtpException ex)
        {
            _logger.LogWarning(ex, "SMTP send failed with status {StatusCode}", ex.StatusCode);
            var errorMessage = ex.InnerException is null ? ex.Message : $"{ex.Message} {ex.InnerException.Message}";
            return SmtpOperationResult.Fail(errorMessage);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _logger.LogWarning(ex, "Email send failed");
            return SmtpOperationResult.Fail(ex.Message);
        }
    }

    private static string RenderTemplate(string template, EmployeeCertificateRecord record)
    {
        var assessmentYear = string.IsNullOrWhiteSpace(record.AssessmentYearDisplay) ? "2026-27" : record.AssessmentYearDisplay;
        return template
            .Replace("{{EmployeeCode}}", record.EmployeeCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{{EmployeeName}}", record.EmployeeName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{AssessmentYear}}", assessmentYear, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadReplyAsync(StreamReader reader)
    {
        var builder = new StringBuilder();
        string? line;
        do
        {
            line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(line);
        }
        while (line.Length > 3 && line[3] == '-');

        return builder.ToString();
    }
}

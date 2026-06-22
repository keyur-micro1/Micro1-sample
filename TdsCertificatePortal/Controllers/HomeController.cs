using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TdsCertificatePortal.Models;
using TdsCertificatePortal.Models.ViewModels;
using TdsCertificatePortal.Services;

namespace TdsCertificatePortal.Controllers;

public class HomeController : Controller
{
    private readonly CertificateZipService _certificateZipService;
    private readonly CsvEmployeeParser _csvEmployeeParser;
    private readonly MappingService _mappingService;
    private readonly ReportService _reportService;
    private readonly SmtpEmailService _smtpEmailService;
    private readonly TempFileService _tempFileService;

    public HomeController(
        CertificateZipService certificateZipService,
        CsvEmployeeParser csvEmployeeParser,
        MappingService mappingService,
        ReportService reportService,
        SmtpEmailService smtpEmailService,
        TempFileService tempFileService)
    {
        _certificateZipService = certificateZipService;
        _csvEmployeeParser = csvEmployeeParser;
        _mappingService = mappingService;
        _reportService = reportService;
        _smtpEmailService = smtpEmailService;
        _tempFileService = tempFileService;
    }

    public IActionResult Index()
    {
        var state = HttpContext.Session.GetPortalState();
        return View(BuildViewModel(state));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveSmtp(SmtpSettings settings)
    {
        var state = HttpContext.Session.GetPortalState();
        state.SmtpSettings = MergeSmtpSettings(settings, state.SmtpSettings);
        HttpContext.Session.SetPortalState(state);
        TempData["Success"] = "SMTP settings saved for the current session.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSmtpConnection(SmtpSettings settings, CancellationToken cancellationToken)
    {
        var state = HttpContext.Session.GetPortalState();
        settings = MergeSmtpSettings(settings, state.SmtpSettings);
        var result = await _smtpEmailService.ValidateConnectionAsync(settings, cancellationToken);
        if (result.Success)
        {
            state.SmtpSettings = settings;
            HttpContext.Session.SetPortalState(state);
            TempData["Success"] = result.Message;
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(SmtpSettings settings, CancellationToken cancellationToken)
    {
        var state = HttpContext.Session.GetPortalState();
        settings = MergeSmtpSettings(settings, state.SmtpSettings);
        var result = await _smtpEmailService.SendTestEmailAsync(settings, cancellationToken);
        if (result.Success)
        {
            state.SmtpSettings = settings;
            HttpContext.Session.SetPortalState(state);
            TempData["Success"] = $"Test email sent. {result.Message}";
        }
        else
        {
            TempData["Error"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessFiles(IFormFile? zipFile, IFormFile? csvFile, CancellationToken cancellationToken)
    {
        var existingState = HttpContext.Session.GetPortalState();
        if (existingState.SmtpSettings is null)
        {
            TempData["Error"] = "Configure SMTP settings before uploading files.";
            return RedirectToAction(nameof(Index));
        }

        if (zipFile is null || zipFile.Length == 0 || csvFile is null || csvFile.Length == 0)
        {
            TempData["Error"] = "Upload both the ZIP file and employee CSV file.";
            return RedirectToAction(nameof(Index));
        }

        _tempFileService.DeleteSessionFolder(existingState.TempFolderPath);
        var tempFolder = _tempFileService.CreateSessionFolder(HttpContext.Session.Id);
        string? savedZipPath = null;
        string? savedCsvPath = null;

        try
        {
            savedZipPath = await _tempFileService.SaveUploadAsync(zipFile, tempFolder, ".zip", cancellationToken);
            savedCsvPath = await _tempFileService.SaveUploadAsync(csvFile, tempFolder, ".csv", cancellationToken);

            var csvResult = await _csvEmployeeParser.ParseAsync(savedCsvPath, cancellationToken);
            if (csvResult.Errors.Count > 0)
            {
                TempData["Error"] = string.Join(" ", csvResult.Errors);
                return RedirectToAction(nameof(Index));
            }

            var zipResult = await _certificateZipService.ExtractAsync(savedZipPath, tempFolder, cancellationToken);
            var state = _mappingService.BuildSessionState(
                existingState.SmtpSettings,
                tempFolder,
                csvResult.Employees,
                zipResult.Certificates,
                zipResult.Issues);
            state.TotalPdfFiles = zipResult.TotalPdfFiles;
            HttpContext.Session.SetPortalState(state);

            TempData["Success"] = $"Processed {csvResult.Employees.Count} employees and {zipResult.TotalPdfFiles} PDF files.";
        }
        catch (InvalidOperationException ex)
        {
            _tempFileService.DeleteSessionFolder(tempFolder);
            TempData["Error"] = ex.Message;
        }
        finally
        {
            DeleteIfExists(savedZipPath);
            DeleteIfExists(savedCsvPath);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult PreviewPdf(Guid id)
    {
        var record = FindRecord(id);
        if (record?.PdfFilePath is null || !System.IO.File.Exists(record.PdfFilePath))
        {
            return NotFound("PDF is no longer available. Reprocess the files if preview is required.");
        }

        Response.Headers.ContentDisposition = $"inline; filename=\"{record.PdfFileName}\"";
        return PhysicalFile(record.PdfFilePath, "application/pdf", enableRangeProcessing: true);
    }

    [HttpGet]
    public IActionResult DownloadPdf(Guid id)
    {
        var record = FindRecord(id);
        if (record?.PdfFilePath is null || !System.IO.File.Exists(record.PdfFilePath))
        {
            return NotFound("PDF is no longer available.");
        }

        return PhysicalFile(record.PdfFilePath, "application/pdf", record.PdfFileName ?? "certificate.pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(Guid id, CancellationToken cancellationToken)
    {
        var state = HttpContext.Session.GetPortalState();
        var record = state.Records.FirstOrDefault(r => r.Id == id);
        if (state.SmtpSettings is null)
        {
            return Json(new { success = false, status = PortalStatuses.Failed, message = "SMTP settings are not configured." });
        }

        if (record is null)
        {
            return Json(new { success = false, status = PortalStatuses.Failed, message = "Employee record was not found." });
        }

        if (!record.CanSend)
        {
            record.DeliveryStatus = PortalStatuses.Skipped;
            record.ErrorMessage = $"Record status is {record.Status}.";
            HttpContext.Session.SetPortalState(state);
            return Json(SendResponse(record, false, record.ErrorMessage));
        }

        record.Status = PortalStatuses.Sending;
        record.DeliveryStatus = PortalStatuses.Sending;
        record.ErrorMessage = null;
        HttpContext.Session.SetPortalState(state);

        var result = await _smtpEmailService.SendCertificateAsync(
            state.SmtpSettings,
            record,
            state.SubjectTemplate,
            state.BodyTemplate,
            cancellationToken);

        if (result.Success)
        {
            record.Status = PortalStatuses.Sent;
            record.DeliveryStatus = PortalStatuses.Sent;
            record.SentAtUtc = DateTimeOffset.UtcNow;
            record.StatusDetail = "Email sent successfully.";
            record.ErrorMessage = null;
        }
        else
        {
            record.Status = PortalStatuses.Failed;
            record.DeliveryStatus = PortalStatuses.Failed;
            record.ErrorMessage = result.Message;
            record.StatusDetail = result.Message;
        }

        HttpContext.Session.SetPortalState(state);
        return Json(SendResponse(record, result.Success, result.Message));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateTemplate(string subjectTemplate, string bodyTemplate)
    {
        var state = HttpContext.Session.GetPortalState();
        state.SubjectTemplate = string.IsNullOrWhiteSpace(subjectTemplate) ? state.SubjectTemplate : subjectTemplate.Trim();
        state.BodyTemplate = string.IsNullOrWhiteSpace(bodyTemplate) ? state.BodyTemplate : bodyTemplate.Trim();
        HttpContext.Session.SetPortalState(state);
        TempData["Success"] = "Email template updated for the current session.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult ValidateDryRun()
    {
        var state = HttpContext.Session.GetPortalState();
        return Json(new
        {
            summary = state.BuildSummary(),
            blockingRecords = state.Records
                .Where(r => !r.CanSend && r.Status != PortalStatuses.Sent)
                .Select(r => new { r.EmployeeCode, r.EmployeeName, r.PanNumber, r.Status, r.StatusDetail })
        });
    }

    [HttpGet]
    public IActionResult Report(string type, string format)
    {
        var state = HttpContext.Session.GetPortalState();
        var report = string.Equals(type, "delivery", StringComparison.OrdinalIgnoreCase)
            ? _reportService.BuildDeliveryReport(state.Records, format)
            : _reportService.BuildMappingReport(state.Records, format);

        return File(report.Bytes, report.ContentType, report.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cleanup()
    {
        var state = HttpContext.Session.GetPortalState();
        _tempFileService.DeleteSessionFolder(state.TempFolderPath);
        HttpContext.Session.ClearPortalState();
        HttpContext.Session.Clear();
        TempData["Success"] = "Session data and temporary files were removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult TempStatus()
    {
        var state = HttpContext.Session.GetPortalState();
        var status = _tempFileService.InspectFolder(state.TempFolderPath);
        return Json(new { status.Exists, status.FileCount, status.Bytes, state.TempFolderPath });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private EmployeeCertificateRecord? FindRecord(Guid id)
    {
        var state = HttpContext.Session.GetPortalState();
        return state.Records.FirstOrDefault(r => r.Id == id);
    }

    private static object SendResponse(EmployeeCertificateRecord record, bool success, string message)
    {
        return new
        {
            success,
            id = record.Id,
            status = record.Status,
            deliveryStatus = record.DeliveryStatus,
            message,
            error = record.ErrorMessage,
            sentAt = record.SentAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            canSend = record.CanSend,
            actionLabel = record.Status == PortalStatuses.Sent ? "Resend Email" : "Send Email"
        };
    }

    private static SmtpSettings MergeSmtpSettings(SmtpSettings submitted, SmtpSettings? existing)
    {
        if (existing is not null && string.IsNullOrWhiteSpace(submitted.Password))
        {
            submitted.Password = existing.Password;
        }

        submitted.SenderEmail = submitted.SenderEmail?.Trim() ?? string.Empty;
        submitted.UserName = submitted.UserName?.Trim();
        submitted.SmtpHost = submitted.SmtpHost?.Trim() ?? string.Empty;
        submitted.CcEmail = submitted.CcEmail?.Trim() ?? string.Empty;
        submitted.TestRecipientEmail = submitted.TestRecipientEmail?.Trim();
        return submitted;
    }

    private static IndexViewModel BuildViewModel(PortalSessionState state)
    {
        var smtp = state.SmtpSettings ?? new SmtpSettings();
        return new IndexViewModel
        {
            SmtpSettings = new SmtpSettings
            {
                SenderEmail = smtp.SenderEmail,
                UserName = smtp.UserName,
                SmtpHost = smtp.SmtpHost,
                SmtpPort = smtp.SmtpPort == 0 ? 587 : smtp.SmtpPort,
                EnableSsl = smtp.EnableSsl,
                CcEmail = smtp.CcEmail,
                TestRecipientEmail = smtp.TestRecipientEmail
            },
            HasSmtpSettings = state.SmtpSettings is not null,
            HasRecords = state.Records.Count > 0,
            Records = state.Records.OrderBy(r => r.EmployeeCode).ToList(),
            PdfIssues = state.PdfIssues,
            Summary = state.BuildSummary(),
            SubjectTemplate = state.SubjectTemplate,
            BodyTemplate = state.BodyTemplate,
            Statuses = PortalStatuses.Filterable
        };
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
    }
}

# TDS Certificate Email Distribution Portal

ASP.NET Core MVC (.NET 9) utility for distributing employee Form 16 Part B / TDS Certificate PDFs by email.

## Implemented Scope

- Session-based SMTP configuration with connection validation and test email.
- ZIP upload and PDF extraction without a database.
- CSV employee import with Employee Code retained through listings, templates, and reports.
- PAN-first mapping with employee-name fallback.
- Duplicate PAN, duplicate PDF, invalid filename, missing PDF, and missing email handling.
- PDF preview and download.
- Individual send, resend, send selected, send all, and retry failed email actions.
- Mapping and email delivery reports in CSV and Excel formats.
- Temporary file cleanup on startup, session timeout, and manual session end.
- IIS-ready Release publish output with generated web.config.

## Runtime Notes

- SMTP credentials are held only in ASP.NET Core session memory and are not written to disk.
- Uploaded ZIP/CSV files are deleted after processing; extracted PDFs live only in the session temp folder until cleanup.
- No database, Docker, or Linux dependency is required.

## Blocked Items Observed During Validation

- The provided Gmail SMTP credential was rejected by Gmail with 535 5.7.8 Username and Password not accepted.
- IIS site creation requires an elevated administrator token. The current Codex process is a medium-integrity admin-member token, so IIS configuration is denied unless the UAC elevation prompt is approved.

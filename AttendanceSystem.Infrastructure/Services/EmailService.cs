using System.Net;
using System.Net.Mail;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Outgoing mail over SMTP.
///
/// Settings are read from the database first and fall back to configuration, so an
/// administrator can fix mail without server access while an existing deployment that
/// configured appsettings keeps working untouched.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly AttendanceDbContext _db;
    private readonly Application.Interfaces.ISecretProtector _secrets;

    public EmailService(IConfiguration config, AttendanceDbContext db,
                        Application.Interfaces.ISecretProtector secrets)
    {
        _config = config;
        _db = db;
        _secrets = secrets;
    }

    /// <summary>Resolved mail settings, and whether they are usable at all.</summary>
    private sealed record MailConfig(
        string? Host, int Port, string? Username, string? Password,
        bool EnableSsl, string FromAddress, string FromName, bool Enabled);

    /// <summary>
    /// Database settings win when a host is stored there; configuration fills the gaps.
    /// </summary>
    private async Task<MailConfig> ResolveAsync()
    {
        var s = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync();

        var dbHasHost = !string.IsNullOrWhiteSpace(s?.SmtpHost);

        var host = dbHasHost ? s!.SmtpHost : _config["Smtp:Host"];
        var port = dbHasHost ? s!.SmtpPort
                             : (int.TryParse(_config["Smtp:Port"], out var p) ? p : 587);
        var username = dbHasHost ? s!.SmtpUsername : _config["Smtp:Username"];

        var password = dbHasHost
            ? _secrets.Unprotect(s!.SmtpPasswordEncrypted)
            : _config["Smtp:Password"];

        var ssl = dbHasHost ? s!.SmtpEnableSsl
                            : bool.TryParse(_config["Smtp:EnableSsl"], out var e) && e;

        var from = (dbHasHost ? s!.SmtpFromAddress : _config["Smtp:FromAddress"])
                   ?? username ?? "noreply@attendancesystem.com";

        var fromName = (dbHasHost ? s!.SmtpFromName : null)
                       ?? (string.IsNullOrWhiteSpace(s?.CompanyName)
                            ? "Attendance Management System" : s!.CompanyName);

        // The database switch only governs database settings. A deployment configured purely
        // through appsettings has no row to enable and must not be switched off by its absence.
        var enabled = dbHasHost ? s!.SmtpEnabled : !string.IsNullOrWhiteSpace(host);

        return new MailConfig(host, port, username, password, ssl, from, fromName, enabled);
    }

    /// <summary>
    /// Delivers one message, reporting real failures.
    ///
    /// Every path used to return success — an unconfigured host skipped sending entirely and
    /// a thrown SmtpException was caught and swallowed. Combined with the deliberately vague
    /// "check your email" on the forgot-password screen, that meant a site with no mail server
    /// silently never sent a single reset, and nothing anywhere said so.
    /// </summary>
    private async Task<Result> SendAsync(string toEmail, string subject, string bodyHtml)
    {
        var cfg = await ResolveAsync();

        if (!cfg.Enabled)
            return Result.Failure("Email sending is switched off in Settings.");

        if (string.IsNullOrWhiteSpace(cfg.Host))
            return Result.Failure(
                "No mail server is configured. Set the SMTP details under Settings → Email.");

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(cfg.FromAddress, cfg.FromName);
            message.To.Add(toEmail);
            message.Subject = subject;
            message.Body = bodyHtml;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(cfg.Host, cfg.Port) { EnableSsl = cfg.EnableSsl };
            if (!string.IsNullOrWhiteSpace(cfg.Username) && !string.IsNullOrWhiteSpace(cfg.Password))
                client.Credentials = new NetworkCredential(cfg.Username, cfg.Password);

            await client.SendMailAsync(message);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error($"EmailService.SendAsync to {toEmail}", ex);
            return Result.Failure($"The mail server rejected the message: {ex.Message}");
        }
    }

    public Task<Result> SendTestEmailAsync(string toEmail) =>
        SendAsync(toEmail, "Test message — Attendance Management System",
            "<p>This is a test message from your Attendance Management System.</p>" +
            "<p>If you are reading it, outgoing mail is working and password reset " +
            "messages will reach your staff.</p>");

    public async Task<Result> SendPasswordResetEmailAsync(string toEmail, string resetLink, string token)
    {
        try
        {
            var bodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f6f9; margin: 0; padding: 20px; }}
        .container {{ max-width: 580px; background: #ffffff; padding: 30px; border-radius: 6px; box-shadow: 0 4px 10px rgba(0,0,0,0.05); margin: 0 auto; }}
        .header {{ text-align: center; padding-bottom: 20px; border-bottom: 1px solid #eeeeee; }}
        .title {{ color: #01a9ac; font-size: 22px; margin-top: 10px; font-weight: bold; }}
        .content {{ padding: 20px 0; color: #404E67; font-size: 15px; line-height: 1.6; }}
        .btn {{ display: inline-block; padding: 12px 24px; background-color: #01a9ac; color: #ffffff !important; text-decoration: none; border-radius: 4px; font-weight: bold; margin: 15px 0; }}
        .token-box {{ background: #f8f9fa; border: 1px dashed #01a9ac; padding: 12px; text-align: center; font-family: monospace; font-size: 16px; font-weight: bold; color: #01a9ac; margin: 15px 0; letter-spacing: 1px; }}
        .footer {{ font-size: 12px; color: #adb5bd; text-align: center; margin-top: 25px; border-top: 1px solid #eeeeee; padding-top: 15px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='title'>Attendance Management System</div>
        </div>
        <div class='content'>
            <p>Hello,</p>
            <p>We received a request to reset your password for your Attendance Management System account.</p>
            <p>Click the button below to reset your password:</p>
            <div style='text-align: center;'>
                <a href='{resetLink}' class='btn' target='_blank'>Reset Password</a>
            </div>
            <p>Alternatively, you can copy your Reset Token below and enter it on the password reset page:</p>
            <div class='token-box'>{token}</div>
            <p><small style='color: #6c757d;'>Note: This password reset link and token will expire in 24 hours.</small></p>
            <p>If you did not request a password reset, please ignore this email.</p>
        </div>
        <div class='footer'>
            &copy; {DateTime.Now.Year} Attendance Management System. All rights reserved.
        </div>
    </div>
</body>
</html>";

            // The token is a credential — logging it would let anyone with read access to the
            // log files take over an account. Record only that a mail was attempted.
            AppLogger.Info($"[PasswordResetEmail] Sending password reset mail to {toEmail}.");

            var result = await SendAsync(
                toEmail, "Password Reset Request - Attendance Management System", bodyHtml);

            // Logged at error level when it fails. The caller still tells the requester
            // nothing — saying "no account" would turn the screen into a username oracle —
            // so this log is the only place the failure is visible to anyone.
            if (!result.IsSuccess)
                AppLogger.Error($"[PasswordResetEmail] Could not send to {toEmail}: {result.ErrorMessage}",
                    new InvalidOperationException(result.ErrorMessage ?? "Send failed."));

            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"EmailService.SendPasswordResetEmailAsync to {toEmail}", ex);
            return Result.Failure(ex.Message);
        }
    }
}

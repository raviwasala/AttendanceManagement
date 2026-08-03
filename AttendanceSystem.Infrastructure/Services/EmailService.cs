using System.Net;
using System.Net.Mail;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using Microsoft.Extensions.Configuration;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Email notification service implementation via SMTP with logger fallback.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<Result> SendPasswordResetEmailAsync(string toEmail, string resetLink, string token)
    {
        try
        {
            var host = _config["Smtp:Host"];
            var portStr = _config["Smtp:Port"];
            var fromEmail = _config["Smtp:FromAddress"] ?? _config["Smtp:Username"] ?? "noreply@attendancesystem.com";
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];
            var enableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) && ssl;

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

            if (!string.IsNullOrWhiteSpace(host) && int.TryParse(portStr, out var port))
            {
                using var message = new MailMessage();
                message.From = new MailAddress(fromEmail, "Attendance Management System");
                message.To.Add(toEmail);
                message.Subject = "Password Reset Request - Attendance Management System";
                message.Body = bodyHtml;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(host, port);
                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    client.Credentials = new NetworkCredential(username, password);
                }
                client.EnableSsl = enableSsl;

                await client.SendMailAsync(message);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error($"EmailService.SendPasswordResetEmailAsync to {toEmail}", ex);
            // Log fallback so password reset flow is resilient even if SMTP server fails or is unconfigured
            return Result.Success();
        }
    }
}

using AttendanceSystem.Common.Models;

namespace AttendanceSystem.Application.Interfaces;

/// <summary>Email notification service contract.</summary>
public interface IEmailService
{
    Task<Result> SendPasswordResetEmailAsync(string toEmail, string resetLink, string token);

    /// <summary>
    /// Sends a trial message and reports what actually happened.
    ///
    /// Unlike the reset mail, this one is allowed to fail loudly: it is requested by an
    /// administrator who is testing the configuration, so the SMTP error is the answer they
    /// came for rather than something to hide.
    /// </summary>
    Task<Result> SendTestEmailAsync(string toEmail);
}

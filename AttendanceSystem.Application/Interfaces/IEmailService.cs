using AttendanceSystem.Common.Models;

namespace AttendanceSystem.Application.Interfaces;

/// <summary>Email notification service contract.</summary>
public interface IEmailService
{
    Task<Result> SendPasswordResetEmailAsync(string toEmail, string resetLink, string token);
}

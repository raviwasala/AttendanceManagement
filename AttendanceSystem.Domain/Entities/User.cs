using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>System user who can log in.</summary>
public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// How far this user's approvals reach, when they hold an approve permission at all.
    ///
    /// Defaults to CompanyWide, which is what every user had implicitly before this existed —
    /// so nothing changes for anyone until it is deliberately narrowed. Irrelevant for users
    /// who cannot approve anything; harmless to leave at the default for them.
    /// </summary>
    public ApprovalScope ApprovalScope { get; set; } = ApprovalScope.CompanyWide;
    public bool IsLocked { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    /// <summary>
    /// SHA-256 hash of the remember-me token. The raw token is shown to the client once and
    /// never stored, so a database leak does not hand out usable long-lived credentials.
    /// </summary>
    public string? RememberTokenHash { get; set; }
    public DateTime? RememberTokenExpiresAt { get; set; }

    /// <summary>SHA-256 hash of the password-reset token — same reasoning as <see cref="RememberTokenHash"/>.</summary>
    public string? ResetPasswordTokenHash { get; set; }
    public DateTime? ResetPasswordTokenExpiry { get; set; }

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

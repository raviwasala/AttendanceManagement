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
    public bool IsLocked { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public string? RememberToken { get; set; }
    public string? ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordTokenExpiry { get; set; }

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs;

// ── Authentication ─────────────────────────────────────────────────────────────

public record LoginDto(
    [Required] string Username,
    [Required] string Password,
    bool RememberMe = false);

public record ChangePasswordDto(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword,
    [Required] string ConfirmPassword);

public record ForgotPasswordDto([Required] string Email);

public record ResetPasswordWithTokenDto(
    [Required] string Email,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword,
    [Required] string ConfirmPassword);

// ── User ───────────────────────────────────────────────────────────────────────

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>How far this user's approvals reach. See <c>ApprovalScope</c>.</summary>
    public ApprovalScope ApprovalScope { get; set; } = ApprovalScope.CompanyWide;
    public string ApprovalScopeDisplay =>
        ApprovalScope == ApprovalScope.CompanyWide ? "All departments" : "Assigned departments";
}

/// <summary>
/// Result of a successful authentication.
///
/// <see cref="RememberToken"/> is the one and only time the raw remember-me token is
/// available — only its hash is persisted, so it can never be read back afterwards.
/// It is deliberately kept off <see cref="UserDto"/> so that user-listing endpoints
/// cannot leak other users' tokens.
/// </summary>
public class AuthResultDto
{
    public UserDto User { get; set; } = new();

    /// <summary>Permissions granted to this user, as <c>"{Module}.{Action}"</c> keys.</summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>Raw remember-me token to write to the client, or <c>null</c> when not requested.</summary>
    public string? RememberToken { get; set; }

    /// <summary>Expiry to use for the remember-me cookie, when a token was issued.</summary>
    public DateTime? RememberTokenExpiresAt { get; set; }
}

public class CreateUserDto
{
    [Required, MaxLength(100)] public string Username { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string FullName { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    [Required] public int RoleId { get; set; }
    public int? EmployeeId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Defaults to company-wide, matching how every user behaved before this existed.</summary>
    public ApprovalScope ApprovalScope { get; set; } = ApprovalScope.CompanyWide;
}

public class UpdateUserDto
{
    public int Id { get; set; }
    [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string FullName { get; set; } = string.Empty;
    [Required] public int RoleId { get; set; }
    public int? EmployeeId { get; set; }
    public bool IsActive { get; set; }
    public ApprovalScope ApprovalScope { get; set; } = ApprovalScope.CompanyWide;
}

// ── Role & Permission ──────────────────────────────────────────────────────────

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// True when this role holds any <c>*.Approve</c> permission.
    ///
    /// Exists so the Users screen can hide approval scope for the roles it means nothing for —
    /// most people cannot approve anything, and asking every one of them which departments
    /// they approve for invites a wrong answer to a question that was never theirs.
    /// </summary>
    public bool CanApprove { get; set; }
}

public class PermissionDto
{
    public int Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}

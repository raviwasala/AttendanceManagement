using System.ComponentModel.DataAnnotations;

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

public record ForgotPasswordDto([Required, EmailAddress] string Email);

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
}

public class UpdateUserDto
{
    public int Id { get; set; }
    [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string FullName { get; set; } = string.Empty;
    [Required] public int RoleId { get; set; }
    public int? EmployeeId { get; set; }
    public bool IsActive { get; set; }
}

// ── Role & Permission ──────────────────────────────────────────────────────────

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class PermissionDto
{
    public int Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}

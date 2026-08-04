using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

public class EmployeeDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime JoiningDate { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public byte[]? Photo { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int DesignationId { get; set; }
    public string DesignationName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The id this employee is enrolled under on the fingerprint devices. Biometric imports
    /// match punches on this value, so an employee without one is silently absent from every
    /// import — which is why it belongs on the form and not only in the database.
    /// </summary>
    public int? BiometricEnrollId { get; set; }
}

public class SaveEmployeeDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [EmailAddress, MaxLength(200)] public string? Email { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    [Required] public DateTime JoiningDate { get; set; }
    public string? Gender { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    public byte[]? Photo { get; set; }
    [Required] public int DepartmentId { get; set; }
    [Required] public int DesignationId { get; set; }
    [Required] public int BranchId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Device enrolment id. Null means the employee is not enrolled on any device.</summary>
    public int? BiometricEnrollId { get; set; }
}

public class EmployeeListItemDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Surfaced in the list so a missing enrolment is visible without opening each record.</summary>
    public int? BiometricEnrollId { get; set; }
}

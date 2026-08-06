using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

// ── Branch ─────────────────────────────────────────────────────────────────────

public class BranchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}

public class SaveBranchDto
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Address { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Department ─────────────────────────────────────────────────────────────────

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int EmployeeCount { get; set; }

    /// <summary>Who runs the department. Optional.</summary>
    public int? HeadEmployeeId { get; set; }
    public string? HeadName { get; set; }

    /// <summary>
    /// Users who may decide leave and overtime for this department, beyond the head.
    ///
    /// Naming anyone here <em>restricts</em> them to this department. A department with no
    /// head and no approvers is still covered by whoever approves company-wide, so a request
    /// can never become undecidable.
    /// </summary>
    public List<DepartmentApproverDto> Approvers { get; set; } = new();
}

public class DepartmentApproverDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class SaveDepartmentDto
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int? HeadEmployeeId { get; set; }
}

public class SaveDepartmentApproverDto
{
    [Required] public int DepartmentId { get; set; }
    [Required] public int UserId { get; set; }
}

// ── Designation ────────────────────────────────────────────────────────────────

public class DesignationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class SaveDesignationDto
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

public class AttendancePeriodLockDto
{
    public int Id { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string RangeDisplay => $"{FromDate:dd-MMM-yyyy} – {ToDate:dd-MMM-yyyy}";

    public int? BranchId { get; set; }
    public string BranchName { get; set; } = "All branches";

    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? LockedByName { get; set; }

    /// <summary>How many attendance records the lock currently covers — what it is protecting.</summary>
    public int RecordCount { get; set; }
}

public class LockPeriodDto
{
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }

    /// <summary>Null locks every branch.</summary>
    public int? BranchId { get; set; }

    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
}

public class UnlockPeriodDto
{
    [Required] public int Id { get; set; }

    /// <summary>Required. Reopening a period that payroll has already run on needs a stated reason.</summary>
    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────────────────────────
// Reprocess
// ──────────────────────────────────────────────────────────────────────────────

public class ReprocessRequestDto
{
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    public int? DepartmentId { get; set; }
    public int? EmployeeId { get; set; }

    /// <summary>
    /// Recalculate rows somebody corrected by hand as well. Off by default: a manual
    /// correction is a human decision about a specific day, and a bulk recalculation
    /// overwriting it is exactly what the manual flag exists to prevent.
    /// </summary>
    public bool IncludeManual { get; set; }
}

public class ReprocessResultDto
{
    public int Examined { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int SkippedManual { get; set; }
    public int SkippedLocked { get; set; }
    public int SkippedNoShift { get; set; }
    public List<string> Warnings { get; set; } = new();
}

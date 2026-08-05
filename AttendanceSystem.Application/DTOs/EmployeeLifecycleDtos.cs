using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// History
// ──────────────────────────────────────────────────────────────────────────────

public class EmployeeHistoryDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public EmployeeChangeType ChangeType { get; set; }
    public string ChangeTypeDisplay => ChangeType.ToString();
    public DateTime EffectiveDate { get; set; }
    public string EffectiveDateDisplay => EffectiveDate.ToString("dd-MMM-yyyy");

    public string? FromLabel { get; set; }
    public string? ToLabel { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }

    public EmployeeStatus? FromStatus { get; set; }
    public EmployeeStatus? ToStatus { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? RecordedBy { get; set; }
}

/// <summary>
/// Moving an employee. Every target is optional — a promotion changes the designation only,
/// a branch move changes the branch only — and at least one must differ from what is on
/// record, or there is nothing to transfer.
/// </summary>
public class TransferEmployeeDto
{
    [Required] public int EmployeeId { get; set; }
    public int? DepartmentId { get; set; }
    public int? DesignationId { get; set; }
    public int? BranchId { get; set; }

    [Required] public DateTime EffectiveDate { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
}

public class ChangeEmployeeStatusDto
{
    [Required] public int EmployeeId { get; set; }
    [Required] public EmployeeStatus Status { get; set; }
    [Required] public DateTime EffectiveDate { get; set; }

    /// <summary>Required. "Inactive with no reason" is precisely what this replaces.</summary>
    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)] public string? Notes { get; set; }
}

public class ResignEmployeeDto
{
    [Required] public int EmployeeId { get; set; }

    /// <summary>Last working day — not the day the resignation was handed in.</summary>
    [Required] public DateTime ResignationDate { get; set; }

    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Notes { get; set; }

    /// <summary>Terminated rather than resigned. Recorded distinctly; treated the same operationally.</summary>
    public bool IsTermination { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// Documents
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Document metadata. Never carries the bytes — a list of twenty would be megabytes.</summary>
public class EmployeeDocumentDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public EmployeeDocumentType DocumentType { get; set; }
    public string DocumentTypeDisplay => DocumentType.ToString();
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeDisplay => SizeBytes < 1024 ? SizeBytes + " B"
                               : SizeBytes < 1048576 ? (SizeBytes / 1024.0).ToString("0.#") + " KB"
                               : (SizeBytes / 1048576.0).ToString("0.#") + " MB";
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>True when the document has lapsed — a work permit nobody renewed.</summary>
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;
}

// ──────────────────────────────────────────────────────────────────────────────
// Profile
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Everything the profile screen shows, in one response rather than six requests.</summary>
public class EmployeeProfileDto
{
    public EmployeeDto Employee { get; set; } = new();

    public EmployeeStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public DateTime? ResignationDate { get; set; }
    public string? ResignationReason { get; set; }

    /// <summary>Shift in force today, or null when nobody assigned one.</summary>
    public string? CurrentShift { get; set; }
    public string? CurrentShiftTimes { get; set; }

    public int ServiceYears { get; set; }
    public int ServiceMonths { get; set; }

    public List<EmployeeHistoryDto> History { get; set; } = new();
    public List<EmployeeDocumentDto> Documents { get; set; } = new();
    public List<MyLeaveBalanceDto> LeaveBalances { get; set; } = new();

    /// <summary>Attendance for the current month, so the profile answers "how are they doing".</summary>
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }
    public int LeaveDays { get; set; }
}

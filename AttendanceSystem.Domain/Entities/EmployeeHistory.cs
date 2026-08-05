using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A dated record of something changing about an employee — a transfer, a change of status,
/// a resignation.
///
/// This exists because the employee row is a snapshot of *now*. Editing someone's department
/// silently rewrites the past: attendance and overtime reports group by the employee's current
/// department, so moving one person from Production to Packing re-attributes every month of
/// their history to a department they did not work in. The figures change, nothing records
/// why, and nobody notices until two reports of the same month disagree.
///
/// One table rather than three. A transfer, a suspension and a resignation are the same shape
/// — "on this date, this changed, for this reason" — and keeping them together means the
/// profile shows one chronological story instead of three lists that have to be merged.
/// </summary>
public class EmployeeHistory : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public EmployeeChangeType ChangeType { get; set; }

    /// <summary>
    /// The date the change takes effect, which is not the date it was entered. A resignation
    /// is usually recorded before the last working day, and a transfer is often backdated.
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    // Only the fields that actually changed are populated; the rest stay null so the history
    // reads as "what moved", not a full copy of the row on every entry.
    public int? FromDepartmentId { get; set; }
    public int? ToDepartmentId { get; set; }
    public int? FromDesignationId { get; set; }
    public int? ToDesignationId { get; set; }
    public int? FromBranchId { get; set; }
    public int? ToBranchId { get; set; }

    public EmployeeStatus? FromStatus { get; set; }
    public EmployeeStatus? ToStatus { get; set; }

    /// <summary>Why. Required for a status change — "inactive" with no reason is what this replaces.</summary>
    public string? Reason { get; set; }
    public string? Notes { get; set; }

    // Names are captured at the time of the change, so the entry still reads correctly after a
    // department is renamed or soft-deleted. A join to a deleted lookup returns nothing, and a
    // history entry that cannot say where somebody moved is worthless.
    public string? FromLabel { get; set; }
    public string? ToLabel { get; set; }
}

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A closed attendance period. Records inside it cannot be edited, and an import will not
/// rewrite them.
///
/// Without this, a month can be checked, signed off and paid — and then silently changed. Two
/// specific things make that likely rather than theoretical: the importer <em>refreshes</em>
/// days it has seen before (so re-running an old file rewrites a paid month), and the review
/// screen will edit any date at all. The result is that the monthly report printed last week
/// may not match the one printed today, with nothing to say which was right.
///
/// Unlocking is a soft delete of the lock, so the audit trail keeps who closed the period,
/// who reopened it and why. A period whose lock has been removed was still, verifiably, closed
/// at the time payroll ran.
/// </summary>
public class AttendancePeriodLock : BaseEntity
{
    /// <summary>First locked date, inclusive.</summary>
    public DateTime FromDate { get; set; }

    /// <summary>Last locked date, inclusive.</summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Null locks every branch. Sites that run payroll per branch close one at a time, and a
    /// single company-wide lock would either close branches that are not ready or leave the
    /// finished ones open.
    /// </summary>
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Why the period was closed — "March payroll paid".</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Recorded when the lock is removed, so reopening a paid month is not silent.</summary>
    public string? UnlockReason { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public int? UnlockedBy { get; set; }
}

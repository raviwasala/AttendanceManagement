using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// One employee's overtime for one day: what attendance produced, what the rule allowed, and
/// what a human finally approved.
///
/// Kept separate from AttendanceLog because the three numbers can legitimately differ and all
/// three need to survive. Attendance says the person worked 97 minutes past shift end; the rule
/// rounds that to 90; the supervisor approves 60 because half of it was not authorised. Payroll
/// needs the last figure and an auditor needs the first two.
/// </summary>
public class OvertimeRecord : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime OvertimeDate { get; set; }

    /// <summary>The attendance row this was derived from. Null for a manually added claim.</summary>
    public int? AttendanceLogId { get; set; }
    public AttendanceLog? AttendanceLog { get; set; }

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    /// <summary>Minutes as attendance calculated them, before any rule was applied.</summary>
    public int RawMinutes { get; set; }

    /// <summary>Minutes after the rule's minimum, cap and rounding — what is being claimed.</summary>
    public int ClaimedMinutes { get; set; }

    /// <summary>
    /// Minutes actually granted. Null until decided; an approver may allow fewer than claimed.
    /// </summary>
    public int? ApprovedMinutes { get; set; }

    public int? OvertimeRuleId { get; set; }
    public OvertimeRule? OvertimeRule { get; set; }

    /// <summary>
    /// Rule name and multiplier as they were when the claim was decided. Copied rather than
    /// looked up so that editing a rule later cannot silently restate what was already paid.
    /// </summary>
    public string? RuleName { get; set; }
    public decimal RateMultiplier { get; set; } = 1m;

    /// <summary>The kind of day this fell on, decided at generation time.</summary>
    public OvertimeDayType DayType { get; set; } = OvertimeDayType.Any;

    public OvertimeStatus Status { get; set; } = OvertimeStatus.Pending;
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Remarks { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Entered by a person rather than derived from attendance.</summary>
    public bool IsManual { get; set; }

    /// <summary>Granted minutes weighted by the rate — the figure payroll multiplies by an hourly rate.</summary>
    public decimal WeightedHours =>
        Math.Round((ApprovedMinutes ?? 0) / 60m * RateMultiplier, 2);
}

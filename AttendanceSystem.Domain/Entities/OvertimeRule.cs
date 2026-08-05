using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Policy that turns raw overtime minutes into a payable claim.
///
/// The shift decides *whether* minutes past the end count as overtime and from when; a rule
/// decides what those minutes are worth and what is allowed. Keeping the two apart means the
/// finance policy can change — a new holiday multiplier, a lower daily cap — without anyone
/// touching a shift definition that hundreds of employees depend on.
/// </summary>
public class OvertimeRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Lower wins when more than one rule matches a day. Gives a specific rule (say, holidays
    /// in Production) a way to beat the catch-all without depending on row order.
    /// </summary>
    public int Priority { get; set; } = 100;

    // ── Scope. Null means "no restriction on this dimension". ────────────────────
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public OvertimeDayType DayType { get; set; } = OvertimeDayType.Any;

    // ── Entitlement ─────────────────────────────────────────────────────────────

    /// <summary>Pay multiplier for these hours — 1.5 for time-and-a-half, 2.0 for double.</summary>
    public decimal RateMultiplier { get; set; } = 1.5m;

    /// <summary>
    /// Overtime shorter than this earns nothing. Stops a few minutes of late departure being
    /// claimed as overtime every day.
    /// </summary>
    public int MinimumMinutes { get; set; } = 30;

    /// <summary>Upper limit per day. Null means no cap.</summary>
    public int? MaxMinutesPerDay { get; set; }

    /// <summary>
    /// Round the claim down to a whole block of this many minutes. 0 disables rounding.
    /// Rounding down, not to nearest: paying for time not worked is harder to defend than
    /// the reverse, and the minimum above already covers the trivial case.
    /// </summary>
    public int RoundToMinutes { get; set; } = 15;

    /// <summary>
    /// When false, claims under this rule are approved as they are generated. Sites that treat
    /// rostered overtime as pre-authorised can skip the queue.
    /// </summary>
    public bool RequiresApproval { get; set; } = true;
}

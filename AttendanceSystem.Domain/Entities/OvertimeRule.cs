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

    /// <summary>
    /// Short identifier for the rule, shown on the payslip line and in exports.
    /// Names change; a code is what a payroll clerk matches against.
    /// </summary>
    public string? Code { get; set; }

    // ── The hourly rate ────────────────────────────────────────────────────────
    //
    // Overtime is paid at (monthly earnings ÷ (Days × Hours)) × RateMultiplier. Both divisors
    // are held here and both are nullable: null means "use the branch's figures", so a site
    // with one convention sets it once and a rule that genuinely differs says so explicitly.
    //
    // Kept per rule rather than only per branch because that is how it is actually used —
    // a holiday rate can be worked out on a different notional day length from ordinary
    // overtime, and forcing one figure on both would misprice one of them.

    /// <summary>Working days in a month for this rule's hourly rate. Null uses the branch figure.</summary>
    public int? DaysPerMonth { get; set; }

    /// <summary>Working hours in a day for this rule's hourly rate. Null uses the branch figure.</summary>
    public decimal? HoursPerDay { get; set; }

    // ── Payroll treatment ──────────────────────────────────────────────────────
    //
    // How the money this rule produces is treated once it reaches a payslip. Independent of
    // each other and of everything above: the multiplier decides *how much*, these decide
    // *what it counts as*. Sri Lankan practice varies — overtime is normally taxable but
    // often outside EPF, and some sites keep it out of the reported gross entirely.

    /// <summary>Overtime earned under this rule enters APIT (PAYE) taxable earnings.</summary>
    public bool IsApitLiable { get; set; } = true;

    /// <summary>
    /// Enters EPF and ETF earnings. False by default — overtime is customarily outside EPF,
    /// and defaulting it true would quietly raise every contribution.
    /// </summary>
    public bool IsEpfLiable { get; set; }

    /// <summary>Counts toward the gross pay figure shown and reported.</summary>
    public bool IncludeInGrossPay { get; set; } = true;
}

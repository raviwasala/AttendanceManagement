namespace AttendanceSystem.Domain.Entities;

/// <summary>Work shift definition.</summary>
public class Shift : BaseEntity
{
    /// <summary>Short code used on rosters and reports, e.g. "GEN", "NGT".</summary>
    public string? ShiftCode { get; set; }

    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    /// <summary>Minutes after StartTime before an arrival counts as late.</summary>
    public int GraceMinutes { get; set; }

    /// <summary>
    /// Minutes before EndTime that a departure still does not count as early. Separate from
    /// GraceMinutes because sites commonly tolerate a late arrival but not an early exit.
    /// </summary>
    public int GraceOutMinutes { get; set; }

    /// <summary>
    /// True when the shift runs past midnight (22:00–06:00). The check-out then falls on the
    /// following calendar day, and every duration calculation has to add a day to the expected
    /// end — otherwise working hours come out negative.
    /// </summary>
    public bool IsNightShift { get; set; }

    /// <summary>Unpaid break deducted from worked time.</summary>
    public int BreakMinutes { get; set; }

    /// <summary>
    /// Paid hours in a normal day, used as the overtime threshold. Falls back to the shift
    /// span minus the break when left at zero.
    /// </summary>
    public double StandardWorkingHours { get; set; }

    /// <summary>
    /// Minutes past the shift end before overtime starts accruing — stops a few minutes of
    /// tidying up being paid as overtime. Used when <see cref="OtCountsFromShiftEnd"/> is true.
    /// </summary>
    public int OtStartAfterMinutes { get; set; }

    /// <summary>
    /// How overtime is measured. True: time worked beyond the shift end plus the threshold.
    /// False: time worked beyond <see cref="EffectiveStandardHours"/>, whenever it happened.
    /// </summary>
    public bool OtCountsFromShiftEnd { get; set; } = true;

    /// <summary>Overtime is only recorded for shifts that allow it.</summary>
    public bool IsOtEnabled { get; set; } = true;

    public string WeeklyOffDays { get; set; } = "Saturday,Sunday";
    public bool IsActive { get; set; } = true;

    public ICollection<EmployeeShift> EmployeeShifts { get; set; } = new List<EmployeeShift>();

    /// <summary>
    /// The shift's span in hours, accounting for a night shift crossing midnight —
    /// 22:00–06:00 is 8 hours, not −16.
    /// </summary>
    public double SpanHours =>
        (EndTime > StartTime ? EndTime - StartTime : EndTime.Add(TimeSpan.FromDays(1)) - StartTime)
        .TotalHours;

    /// <summary>Paid hours per day — the explicit value when set, otherwise span minus break.</summary>
    public double EffectiveStandardHours =>
        StandardWorkingHours > 0 ? StandardWorkingHours : Math.Max(0, SpanHours - BreakMinutes / 60.0);

    /// <summary>
    /// True when the clock times imply a crossing of midnight, regardless of the flag.
    /// Used to warn on save if the flag and the times disagree.
    /// </summary>
    public bool TimesCrossMidnight => EndTime <= StartTime;
}

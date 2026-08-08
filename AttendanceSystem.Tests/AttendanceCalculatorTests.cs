using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using Xunit;

namespace AttendanceSystem.Tests;

/// <summary>
/// Attendance feeds payroll. Late minutes, working hours and overtime minutes computed here
/// become no-pay days and OT money on a payslip, so an error in this file is not a reporting
/// problem — it is somebody's wrong wages, arrived at confidently.
///
/// The night-shift cases are the ones that matter most. Six of this site's shifts cross
/// midnight, and every one of them makes the out-punch land on the following calendar day.
/// </summary>
public class AttendanceCalculatorTests
{
    private static Shift Day(
        string start = "09:00", string end = "17:00",
        int grace = 15, int graceOut = 15, int breakMins = 60,
        double standardHours = 8, bool otEnabled = true,
        bool otFromShiftEnd = true, int otAfter = 0,
        string weeklyOff = "Saturday,Sunday") => new()
    {
        Name = "Day",
        StartTime = TimeSpan.Parse(start),
        EndTime = TimeSpan.Parse(end),
        GraceMinutes = grace,
        GraceOutMinutes = graceOut,
        BreakMinutes = breakMins,
        StandardWorkingHours = standardHours,
        IsOtEnabled = otEnabled,
        OtCountsFromShiftEnd = otFromShiftEnd,
        OtStartAfterMinutes = otAfter,
        WeeklyOffDays = weeklyOff
    };

    private static Shift Night() => new()
    {
        Name = "Night",
        StartTime = TimeSpan.Parse("22:00"),
        EndTime = TimeSpan.Parse("06:00"),
        IsNightShift = true,
        GraceMinutes = 15,
        GraceOutMinutes = 15,
        BreakMinutes = 60,
        StandardWorkingHours = 8,
        IsOtEnabled = true,
        OtCountsFromShiftEnd = true,
        WeeklyOffDays = "Sunday"
    };

    // A Wednesday, so it is neither a weekly off nor adjacent to one by accident.
    private static readonly DateTime Wed = new(2026, 8, 5);

    private static DateTime At(DateTime day, string time) => day.Add(TimeSpan.Parse(time));

    // ── No punch ──────────────────────────────────────────────────────────────

    [Fact]
    public void No_check_in_on_a_working_day_is_absent()
    {
        var r = AttendanceCalculator.Calculate(Day(), Wed, null, null, isHoliday: false);
        Assert.Equal(AttendanceStatus.Absent, r.Status);
    }

    [Fact]
    public void No_check_in_on_a_weekly_off_is_not_absent()
    {
        // A Saturday. Counting this as absent would drive a no-pay day for a day nobody was
        // ever expected to work — four unearned no-pay days a month.
        var saturday = new DateTime(2026, 8, 8);
        var r = AttendanceCalculator.Calculate(Day(), saturday, null, null, isHoliday: false);
        Assert.Equal(AttendanceStatus.WeeklyOff, r.Status);
    }

    [Fact]
    public void No_check_in_on_a_holiday_is_not_absent()
    {
        var r = AttendanceCalculator.Calculate(Day(), Wed, null, null, isHoliday: true);
        Assert.Equal(AttendanceStatus.Holiday, r.Status);
    }

    [Fact]
    public void Leave_outranks_holiday_and_weekly_off()
    {
        var r = AttendanceCalculator.Calculate(Day(), Wed, null, null, isHoliday: true, isOnLeave: true);
        Assert.Equal(AttendanceStatus.OnLeave, r.Status);
    }

    // ── Lateness ──────────────────────────────────────────────────────────────

    [Fact]
    public void Arriving_inside_the_grace_period_is_not_late()
    {
        var r = AttendanceCalculator.Calculate(
            Day(grace: 15), Wed, At(Wed, "09:14"), At(Wed, "17:00"), false);

        Assert.False(r.IsLate);
        Assert.Null(r.LateMinutes);
        Assert.Equal(AttendanceStatus.Present, r.Status);
    }

    [Fact]
    public void Late_minutes_are_counted_from_the_end_of_grace_not_from_the_shift_start()
    {
        var r = AttendanceCalculator.Calculate(
            Day(grace: 15), Wed, At(Wed, "09:30"), At(Wed, "17:00"), false);

        Assert.True(r.IsLate);
        Assert.Equal(15, r.LateMinutes);          // not 30
        Assert.Equal(AttendanceStatus.Late, r.Status);
    }

    [Fact]
    public void A_holiday_outranks_lateness_in_the_status()
    {
        // The day describes itself; how the person performed on it does not rename it.
        var r = AttendanceCalculator.Calculate(
            Day(), Wed, At(Wed, "10:30"), At(Wed, "17:00"), isHoliday: true);

        Assert.True(r.IsLate);                        // still recorded
        Assert.Equal(AttendanceStatus.Holiday, r.Status);
    }

    [Fact]
    public void Without_a_shift_nobody_can_be_late()
    {
        var r = AttendanceCalculator.Calculate(null, Wed, At(Wed, "11:00"), At(Wed, "17:00"), false);

        Assert.False(r.IsLate);
        Assert.Null(r.LateMinutes);
    }

    // ── Early leave ───────────────────────────────────────────────────────────

    [Fact]
    public void Leaving_inside_the_out_grace_is_not_early()
    {
        var r = AttendanceCalculator.Calculate(
            Day(graceOut: 15), Wed, At(Wed, "09:00"), At(Wed, "16:50"), false);

        Assert.False(r.IsEarlyLeave);
    }

    [Fact]
    public void Early_minutes_are_counted_from_the_start_of_the_out_grace()
    {
        var r = AttendanceCalculator.Calculate(
            Day(graceOut: 15), Wed, At(Wed, "09:00"), At(Wed, "16:00"), false);

        Assert.True(r.IsEarlyLeave);
        Assert.Equal(45, r.EarlyLeaveMinutes);    // 17:00 − 15m grace − 16:00
    }

    // ── Hours ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Working_hours_are_gross_hours_less_the_break()
    {
        var r = AttendanceCalculator.Calculate(
            Day(breakMins: 60), Wed, At(Wed, "09:00"), At(Wed, "17:00"), false);

        Assert.Equal(8.0, r.GrossHours);
        Assert.Equal(7.0, r.WorkingHours);
    }

    [Fact]
    public void A_shift_shorter_than_its_break_does_not_produce_negative_hours()
    {
        var r = AttendanceCalculator.Calculate(
            Day(breakMins: 60), Wed, At(Wed, "09:00"), At(Wed, "09:30"), false);

        Assert.Equal(0, r.WorkingHours);
    }

    [Fact]
    public void No_check_out_leaves_hours_unknown_rather_than_zero()
    {
        // Zero would read as "worked nothing" and drive a no-pay day. Unknown is the truth,
        // and it is what makes a missing punch show up on Attendance Review.
        var r = AttendanceCalculator.Calculate(Day(), Wed, At(Wed, "09:00"), null, false);

        Assert.Null(r.WorkingHours);
        Assert.Null(r.GrossHours);
    }

    // ── Night shifts ──────────────────────────────────────────────────────────

    [Fact]
    public void A_night_shift_out_punch_on_the_same_date_rolls_forward_a_day()
    {
        // 22:00 → 06:00 recorded against one calendar date. Read literally the employee
        // worked minus sixteen hours.
        var r = AttendanceCalculator.Calculate(
            Night(), Wed, At(Wed, "22:00"), At(Wed, "06:00"), false);

        Assert.Equal(8.0, r.GrossHours);
        Assert.Equal(7.0, r.WorkingHours);
    }

    [Fact]
    public void A_full_night_shift_is_not_reported_as_leaving_early()
    {
        // The regression this guards: comparing a 06:00 out-punch against an expected end on
        // the following day reported 1,440 minutes early — a full night shift flagged as
        // early leave, every single time.
        var r = AttendanceCalculator.Calculate(
            Night(), Wed, At(Wed, "22:00"), At(Wed, "06:00"), false);

        Assert.False(r.IsEarlyLeave);
        Assert.Null(r.EarlyLeaveMinutes);
    }

    [Fact]
    public void Night_shift_overtime_is_measured_past_the_rolled_forward_end()
    {
        // Out at 08:00 against an 06:00 end is two hours of overtime. Comparing a same-date
        // out-punch against an end on the following day understated this by a full day and
        // silently paid nothing.
        var r = AttendanceCalculator.Calculate(
            Night(), Wed, At(Wed, "22:00"), At(Wed, "08:00"), false);

        Assert.Equal(120, r.OvertimeMinutes);
    }

    [Fact]
    public void A_night_shift_leaving_early_is_still_detected()
    {
        // The roll-forward must not make early leave impossible to detect.
        var r = AttendanceCalculator.Calculate(
            Night(), Wed, At(Wed, "22:00"), At(Wed, "04:00"), false);

        Assert.True(r.IsEarlyLeave);
        Assert.Equal(105, r.EarlyLeaveMinutes);   // 06:00 − 15m grace − 04:00
    }

    [Fact]
    public void CrossesMidnight_is_true_when_the_times_say_so_even_without_the_flag()
    {
        // Shifts saved before the flag existed must still calculate correctly.
        var s = Day(start: "22:00", end: "06:00");
        Assert.False(s.IsNightShift);
        Assert.True(AttendanceCalculator.CrossesMidnight(s));
    }

    // ── Overtime ──────────────────────────────────────────────────────────────

    [Fact]
    public void Overtime_from_shift_end_ignores_arriving_early()
    {
        // An hour early and an hour late is one hour of overtime under this rule, not two.
        var r = AttendanceCalculator.Calculate(
            Day(otFromShiftEnd: true), Wed, At(Wed, "08:00"), At(Wed, "18:00"), false);

        Assert.Equal(60, r.OvertimeMinutes);
    }

    [Fact]
    public void Overtime_beyond_standard_hours_counts_time_worked_wherever_it_fell()
    {
        // Same punches, the other rule: 10 hours gross − 1 hour break = 9 worked, one over.
        var r = AttendanceCalculator.Calculate(
            Day(otFromShiftEnd: false, standardHours: 8), Wed,
            At(Wed, "08:00"), At(Wed, "18:00"), false);

        Assert.Equal(60, r.OvertimeMinutes);
    }

    [Fact]
    public void The_overtime_threshold_must_be_passed_before_anything_counts()
    {
        var shift = Day(otFromShiftEnd: true, otAfter: 30);

        var justUnder = AttendanceCalculator.Calculate(
            shift, Wed, At(Wed, "09:00"), At(Wed, "17:20"), false);
        Assert.Equal(0, justUnder.OvertimeMinutes);

        var over = AttendanceCalculator.Calculate(
            shift, Wed, At(Wed, "09:00"), At(Wed, "18:00"), false);
        Assert.Equal(30, over.OvertimeMinutes);   // measured from 17:30
    }

    [Fact]
    public void Every_hour_worked_on_a_weekly_off_is_overtime()
    {
        // Measuring from the shift end would say otherwise: somebody called in for four hours
        // on their Sunday leaves long before the nominal end and would earn nothing.
        var sunday = new DateTime(2026, 8, 9);
        var r = AttendanceCalculator.Calculate(
            Day(weeklyOff: "Saturday,Sunday"), sunday, At(sunday, "09:00"), At(sunday, "14:00"), false);

        Assert.Equal(240, r.OvertimeMinutes);     // 5 hours gross − 1 hour break
    }

    [Fact]
    public void Every_hour_worked_on_a_holiday_is_overtime()
    {
        var r = AttendanceCalculator.Calculate(
            Day(), Wed, At(Wed, "09:00"), At(Wed, "14:00"), isHoliday: true);

        Assert.Equal(240, r.OvertimeMinutes);
    }

    [Fact]
    public void Overtime_is_never_negative()
    {
        var r = AttendanceCalculator.Calculate(
            Day(), Wed, At(Wed, "09:00"), At(Wed, "15:00"), false);

        Assert.Equal(0, r.OvertimeMinutes);
    }

    [Fact]
    public void A_shift_with_overtime_disabled_earns_none_however_long_the_day()
    {
        var r = AttendanceCalculator.Calculate(
            Day(otEnabled: false), Wed, At(Wed, "09:00"), At(Wed, "23:00"), false);

        Assert.Null(r.OvertimeMinutes);
    }

    [Fact]
    public void Overtime_minutes_are_floored_so_a_part_minute_is_never_rounded_up()
    {
        var r = AttendanceCalculator.Calculate(
            Day(), Wed, At(Wed, "09:00"), At(Wed, "17:30:59"), false);

        Assert.Equal(30, r.OvertimeMinutes);
    }

    // ── Weekly off parsing ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Saturday,Sunday", "2026-08-08", true)]
    [InlineData("Saturday,Sunday", "2026-08-05", false)]
    [InlineData("saturday, sunday", "2026-08-09", true)]   // spacing and case
    [InlineData("", "2026-08-09", false)]                  // nothing configured
    public void Weekly_off_matches_day_names_tolerantly(string offDays, string date, bool expected)
    {
        var shift = Day(weeklyOff: offDays);
        Assert.Equal(expected, AttendanceCalculator.IsWeeklyOff(shift, DateTime.Parse(date)));
    }
}

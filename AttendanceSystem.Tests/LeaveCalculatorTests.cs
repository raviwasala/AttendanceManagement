using AttendanceSystem.Application.Services;
using Xunit;

namespace AttendanceSystem.Tests;

/// <summary>
/// How many days a leave request costs. An error here is a day of entitlement per request —
/// in the employee's favour or the company's depending which way it slips — and it compounds
/// silently across a year because nobody recounts a balance by hand.
/// </summary>
public class LeaveCalculatorTests
{
    private static readonly HashSet<DateTime> NoHolidays = [];
    private static bool NeverOff(DateTime d) => false;

    private static bool WeekendOff(DateTime d) =>
        d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    // Monday 3 August 2026 through the following Sunday.
    private static readonly DateTime Mon = new(2026, 8, 3);

    // ── Counting ──────────────────────────────────────────────────────────────

    [Fact]
    public void Both_ends_are_inclusive()
    {
        // "The 3rd to the 5th" is three days, not two. This is the off-by-one that costs a
        // day of entitlement on every request.
        var days = LeaveCalculator.CountWorkingDays(Mon, Mon.AddDays(2), NoHolidays, NeverOff);
        Assert.Equal(3, days);
    }

    [Fact]
    public void A_single_day_request_is_one_day()
    {
        Assert.Equal(1, LeaveCalculator.CountWorkingDays(Mon, Mon, NoHolidays, NeverOff));
    }

    [Fact]
    public void Weekly_off_days_are_not_counted()
    {
        // Monday to Sunday is seven calendar days, five working.
        var days = LeaveCalculator.CountWorkingDays(Mon, Mon.AddDays(6), NoHolidays, WeekendOff);
        Assert.Equal(5, days);
    }

    [Fact]
    public void Holidays_are_not_counted()
    {
        var holidays = new HashSet<DateTime> { Mon.AddDays(1) };   // the Tuesday
        var days = LeaveCalculator.CountWorkingDays(Mon, Mon.AddDays(4), holidays, NeverOff);
        Assert.Equal(4, days);
    }

    [Fact]
    public void A_holiday_falling_on_a_weekly_off_is_not_deducted_twice()
    {
        // Saturday is both. Counting it once as a holiday and again as an off day would
        // subtract two days from a range that only contains one.
        var saturday = Mon.AddDays(5);
        var holidays = new HashSet<DateTime> { saturday };

        var days = LeaveCalculator.CountWorkingDays(Mon, Mon.AddDays(6), holidays, WeekendOff);
        Assert.Equal(5, days);   // still Monday to Friday
    }

    [Fact]
    public void A_request_entirely_inside_a_weekend_costs_nothing()
    {
        var saturday = Mon.AddDays(5);
        var days = LeaveCalculator.CountWorkingDays(saturday, saturday.AddDays(1), NoHolidays, WeekendOff);
        Assert.Equal(0, days);
    }

    [Fact]
    public void A_backwards_range_counts_nothing_rather_than_looping()
    {
        // A request with the dates the wrong way round must not cost zero days and sail
        // through approval — but it must also not hang. Zero, and the caller rejects it.
        var days = LeaveCalculator.CountWorkingDays(Mon.AddDays(5), Mon, NoHolidays, NeverOff);
        Assert.Equal(0, days);
    }

    [Fact]
    public void The_time_of_day_does_not_change_the_count()
    {
        // Dates arrive from a form and can carry a time. Counting must be by date.
        var days = LeaveCalculator.CountWorkingDays(
            Mon.AddHours(17), Mon.AddDays(2).AddHours(9), NoHolidays, NeverOff);

        Assert.Equal(3, days);
    }

    [Fact]
    public void A_month_long_request_counts_every_working_day_in_it()
    {
        // August 2026: 31 days, 21 weekdays.
        var days = LeaveCalculator.CountWorkingDays(
            new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), NoHolidays, WeekendOff);

        Assert.Equal(21, days);
    }

    // ── Overlap ───────────────────────────────────────────────────────────────

    [Theory]
    // Identical
    [InlineData("2026-08-03", "2026-08-07", "2026-08-03", "2026-08-07", true)]
    // One inside the other
    [InlineData("2026-08-03", "2026-08-07", "2026-08-04", "2026-08-05", true)]
    // Partial, either way round
    [InlineData("2026-08-03", "2026-08-07", "2026-08-06", "2026-08-10", true)]
    [InlineData("2026-08-06", "2026-08-10", "2026-08-03", "2026-08-07", true)]
    // Touching on one day — that day is in both, so they DO overlap
    [InlineData("2026-08-03", "2026-08-07", "2026-08-07", "2026-08-10", true)]
    // Adjacent but not touching
    [InlineData("2026-08-03", "2026-08-07", "2026-08-08", "2026-08-10", false)]
    // Far apart
    [InlineData("2026-08-03", "2026-08-07", "2026-09-01", "2026-09-05", false)]
    public void Overlap_is_inclusive_at_both_ends(
        string aFrom, string aTo, string bFrom, string bTo, bool expected)
    {
        Assert.Equal(expected, LeaveCalculator.Overlaps(
            DateTime.Parse(aFrom), DateTime.Parse(aTo),
            DateTime.Parse(bFrom), DateTime.Parse(bTo)));
    }

    [Fact]
    public void Overlap_does_not_depend_on_which_range_is_given_first()
    {
        var a = (From: new DateTime(2026, 8, 3), To: new DateTime(2026, 8, 7));
        var b = (From: new DateTime(2026, 8, 5), To: new DateTime(2026, 8, 10));

        Assert.Equal(
            LeaveCalculator.Overlaps(a.From, a.To, b.From, b.To),
            LeaveCalculator.Overlaps(b.From, b.To, a.From, a.To));
    }

    // ── Balance ───────────────────────────────────────────────────────────────

    [Fact]
    public void Remaining_is_entitlement_less_what_is_committed()
    {
        Assert.Equal(7m, LeaveCalculator.Remaining(14m, 7m));
    }

    [Fact]
    public void An_overdrawn_entitlement_reads_as_zero_not_as_a_negative()
    {
        // A negative balance would be treated as available by anything that only checks
        // "is there a number here".
        Assert.Equal(0m, LeaveCalculator.Remaining(14m, 20m));
    }

    [Fact]
    public void Half_days_survive_the_arithmetic()
    {
        Assert.Equal(6.5m, LeaveCalculator.Remaining(14m, 7.5m));
    }
}

using AttendanceSystem.Application.Services;
using Xunit;

namespace AttendanceSystem.Tests;

/// <summary>
/// Back-pay for a raise that started in a month already paid.
///
/// The count is one lump sum on a payslip that nobody can check without redoing it by hand,
/// so it is worth being exactly right about which months are included.
/// </summary>
public class SalaryIncrementTests
{
    [Theory]
    // Effective in the open month: paid at the new rate directly, nothing owed.
    [InlineData("2026-08-01", 202608, 0)]
    [InlineData("2026-08-31", 202608, 0)]
    // One month back.
    [InlineData("2026-07-01", 202608, 1)]
    [InlineData("2026-07-15", 202608, 1)]   // mid-month still counts its whole month
    // Several, including across a year end.
    [InlineData("2026-05-01", 202608, 3)]
    [InlineData("2025-12-01", 202603, 3)]
    [InlineData("2025-08-01", 202608, 12)]
    // Future-dated: never negative.
    [InlineData("2026-09-01", 202608, 0)]
    [InlineData("2027-01-01", 202608, 0)]
    public void Arrears_months_count_paid_months_before_the_open_one(
        string effective, int openYearMonth, int expected)
    {
        Assert.Equal(expected,
            SalaryIncrementService.ArrearsMonthsBetween(DateTime.Parse(effective), openYearMonth));
    }

    [Fact]
    public void The_open_month_is_excluded_so_a_raise_is_not_paid_twice_for_it()
    {
        // The open month is already being paid at the new basic. Counting it as arrears too
        // would pay that month's increase twice — the mistake this whole helper exists to
        // avoid.
        var july = SalaryIncrementService.ArrearsMonthsBetween(new DateTime(2026, 7, 1), 202608);
        var august = SalaryIncrementService.ArrearsMonthsBetween(new DateTime(2026, 8, 1), 202608);

        Assert.Equal(1, july);
        Assert.Equal(0, august);
    }
}

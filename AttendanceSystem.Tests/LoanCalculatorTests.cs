using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Enums;
using Xunit;

namespace AttendanceSystem.Tests;

/// <summary>
/// A loan instalment is deducted from a payslip untouched, so an error here reaches the
/// employee's pay directly. The property that matters most is the last one tested: the
/// instalments must sum exactly to what is owed, or a loan never quite clears and somebody
/// is still paying it a year after it finished.
/// </summary>
public class LoanCalculatorTests
{
    // ── Interest-free, the normal staff loan ──────────────────────────────────

    [Fact]
    public void An_interest_free_loan_costs_nothing_extra()
    {
        var s = LoanCalculator.Calculate(120_000m, 0m, 12, LoanInterestType.Fixed);

        Assert.Equal(0m, s.InterestAmount);
        Assert.Equal(120_000m, s.TotalPayable);
        Assert.Equal(10_000m, s.MonthlyInstallment);
        Assert.Equal(10_000m, s.FinalInstallment);
    }

    [Fact]
    public void A_zero_rate_does_not_fall_into_the_reducing_balance_formula()
    {
        // The amortisation formula divides by the rate. Reducing balance at 0% must be
        // short-circuited or it produces a division by zero rather than a free loan.
        var s = LoanCalculator.Calculate(120_000m, 0m, 12, LoanInterestType.Reducing);

        Assert.Equal(0m, s.InterestAmount);
        Assert.Equal(10_000m, s.MonthlyInstallment);
    }

    // ── Flat interest ─────────────────────────────────────────────────────────

    [Fact]
    public void Flat_interest_is_charged_on_the_whole_principal_for_the_whole_term()
    {
        // 100,000 at 12% for one year = 12,000, regardless of what has been repaid.
        var s = LoanCalculator.Calculate(100_000m, 12m, 12, LoanInterestType.Fixed);

        Assert.Equal(12_000m, s.InterestAmount);
        Assert.Equal(112_000m, s.TotalPayable);
        Assert.Equal(9_333.33m, s.MonthlyInstallment);
    }

    [Fact]
    public void Flat_interest_scales_with_the_term_not_just_the_rate()
    {
        var oneYear = LoanCalculator.Calculate(100_000m, 12m, 12, LoanInterestType.Fixed);
        var twoYears = LoanCalculator.Calculate(100_000m, 12m, 24, LoanInterestType.Fixed);

        Assert.Equal(12_000m, oneYear.InterestAmount);
        Assert.Equal(24_000m, twoYears.InterestAmount);
    }

    // ── Reducing balance ──────────────────────────────────────────────────────

    [Fact]
    public void Reducing_balance_costs_the_borrower_less_than_flat_at_the_same_rate()
    {
        // The single fact a borrower needs to understand about the two options. If this ever
        // inverts, the interest type has been applied the wrong way round.
        var flat = LoanCalculator.Calculate(100_000m, 12m, 12, LoanInterestType.Fixed);
        var reducing = LoanCalculator.Calculate(100_000m, 12m, 12, LoanInterestType.Reducing);

        Assert.True(reducing.InterestAmount < flat.InterestAmount,
            $"reducing {reducing.InterestAmount} should be less than flat {flat.InterestAmount}");
    }

    [Fact]
    public void Reducing_balance_matches_the_standard_amortisation_figure()
    {
        // 100,000 at 12% over 12 months amortises to 8,884.88 a month — the figure any
        // banking calculator gives. Roughly 6,618 of interest against 12,000 flat.
        var s = LoanCalculator.Calculate(100_000m, 12m, 12, LoanInterestType.Reducing);

        Assert.InRange(s.MonthlyInstallment, 8_884m, 8_886m);
        Assert.InRange(s.InterestAmount, 6_600m, 6_640m);
    }

    // ── The property that matters ─────────────────────────────────────────────

    [Theory]
    [InlineData(100_000, 0, 12, LoanInterestType.Fixed)]
    [InlineData(100_000, 12, 12, LoanInterestType.Fixed)]
    [InlineData(100_000, 12, 12, LoanInterestType.Reducing)]
    [InlineData(50_000, 7.5, 7, LoanInterestType.Reducing)]     // does not divide evenly
    [InlineData(33_333, 0, 7, LoanInterestType.Fixed)]          // nor does this
    [InlineData(10, 0, 3, LoanInterestType.Fixed)]              // tiny, worst case for rounding
    public void Instalments_always_sum_exactly_to_what_is_owed(
        decimal principal, decimal rate, int months, LoanInterestType type)
    {
        var s = LoanCalculator.Calculate(principal, rate, months, type);

        var paid = s.MonthlyInstallment * (months - 1) + s.FinalInstallment;

        Assert.Equal(s.TotalPayable, paid);
    }

    [Fact]
    public void The_last_instalment_absorbs_the_rounding_rather_than_leaving_a_remainder()
    {
        // 100,000 over 7 months is 14,285.714… Rounding every instalment the same way leaves
        // a few cents outstanding forever.
        var s = LoanCalculator.Calculate(100_000m, 0m, 7, LoanInterestType.Fixed);

        Assert.Equal(14_285.71m, s.MonthlyInstallment);
        Assert.NotEqual(s.MonthlyInstallment, s.FinalInstallment);
        Assert.Equal(100_000m, s.MonthlyInstallment * 6 + s.FinalInstallment);
    }

    // ── Nonsense in, nothing out ──────────────────────────────────────────────

    [Theory]
    [InlineData(0, 12)]
    [InlineData(-5_000, 12)]
    [InlineData(100_000, 0)]
    [InlineData(100_000, -3)]
    public void A_loan_that_cannot_exist_returns_an_empty_schedule_rather_than_throwing(
        decimal principal, int months)
    {
        var s = LoanCalculator.Calculate(principal, 10m, months, LoanInterestType.Fixed);

        Assert.Equal(0m, s.TotalPayable);
        Assert.Equal(0m, s.MonthlyInstallment);
    }

    // ── Rescheduling ──────────────────────────────────────────────────────────

    [Fact]
    public void Rescheduling_spreads_what_is_left_without_recomputing_interest()
    {
        // Interest was fixed when the loan was granted. Recalculating it on a restructure
        // would silently change the cost of a loan the borrower already agreed to.
        var s = LoanCalculator.Reschedule(47_500m, 5);

        Assert.Equal(0m, s.InterestAmount);
        Assert.Equal(47_500m, s.TotalPayable);
        Assert.Equal(9_500m, s.MonthlyInstallment);
    }

    [Fact]
    public void A_rescheduled_loan_also_sums_exactly()
    {
        var s = LoanCalculator.Reschedule(10_000m, 3);

        Assert.Equal(10_000m, s.MonthlyInstallment * 2 + s.FinalInstallment);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-100, 5)]
    [InlineData(5_000, 0)]
    public void Rescheduling_nothing_returns_an_empty_schedule(decimal outstanding, int months)
    {
        var s = LoanCalculator.Reschedule(outstanding, months);
        Assert.Equal(0m, s.TotalPayable);
    }
}

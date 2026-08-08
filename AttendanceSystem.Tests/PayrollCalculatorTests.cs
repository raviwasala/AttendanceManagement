using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Enums;
using Xunit;

namespace AttendanceSystem.Tests;

/// <summary>
/// The payroll calculator is the one place in this system where a silent error becomes
/// somebody's wrong wages, discovered by them and not by us. Every figure below is one that
/// can be checked with arithmetic on paper, which is the whole reason the calculator takes
/// plain records instead of a database.
/// </summary>
public class PayrollCalculatorTests
{
    /// <summary>The 2023 monthly APIT table — earnings × rate − relief.</summary>
    private static readonly TaxBandInput[] Bands =
    [
        new() { FromAmount = 0,       ToAmount = 100_000,  RatePercent = 0,  Relief = 0 },
        new() { FromAmount = 100_001, ToAmount = 141_667,  RatePercent = 6,  Relief = 6_000 },
        new() { FromAmount = 141_668, ToAmount = 183_333,  RatePercent = 12, Relief = 14_500 },
        new() { FromAmount = 183_334, ToAmount = 225_000,  RatePercent = 18, Relief = 25_500 },
        new() { FromAmount = 225_001, ToAmount = 266_667,  RatePercent = 24, Relief = 39_000 },
        new() { FromAmount = 266_668, ToAmount = 308_333,  RatePercent = 30, Relief = 55_000 },
        new() { FromAmount = 308_334, ToAmount = null,     RatePercent = 36, Relief = 73_500 }
    ];

    private static PayrollInput Basic(decimal basic = 50_000m) => new()
    {
        Year = 2026, Month = 7,
        WorkingDays = 26, PresentDays = 26,
        BasicSalary = basic,
        TaxBands = Bands,
        DaysPerMonth = 30
    };

    // ── No pay ────────────────────────────────────────────────────────────────

    [Fact]
    public void NoPay_reduces_basic_by_a_day_rate_of_thirtieths()
    {
        var r = PayrollCalculator.Calculate(Basic() with { NoPayDays = 3 });

        // Three days of thirty is exactly a tenth of the salary: 5,000.00.
        //
        // Worth stating why this is not 5,000.01. Rounding the daily rate to 1,666.67 first
        // and then multiplying by three gives a cent more, and that cent is not free — it is
        // charged to the employee every time. The whole reduction is computed at full
        // precision and rounded once, at the end.
        Assert.Equal(5_000.00m, r.NoPayDeduction);
        Assert.Equal(45_000.00m, r.EarnedBasic);
    }

    [Fact]
    public void NoPay_is_divided_by_the_configured_month_not_the_calendar_month()
    {
        // Same salary, same absence, February and March must cost the same. Dividing by the
        // real month length would make a February day dearer than a March one.
        var feb = PayrollCalculator.Calculate(Basic() with { Month = 2, NoPayDays = 1 });
        var mar = PayrollCalculator.Calculate(Basic() with { Month = 3, NoPayDays = 1 });

        Assert.Equal(feb.NoPayDeduction, mar.NoPayDeduction);
    }

    [Fact]
    public void NoPay_reduces_only_the_allowances_flagged_for_it()
    {
        var r = PayrollCalculator.Calculate(Basic() with
        {
            NoPayDays = 3,
            Components =
            [
                // Follows attendance
                new() { Code = "A001", Name = "Attendance", Amount = 10_000m, IncludeInNoPay = true },
                // Settled against real journeys — must not be cut for absence as well
                new() { Code = "0004", Name = "Travelling", Amount = 5_000m, IncludeInNoPay = false }
            ]
        });

        // Base is 50,000 + 10,000 = 60,000. Three days = 6,000.
        Assert.Equal(6_000m, r.NoPayDeduction);

        var travelling = r.Lines.Single(l => l.Code == "0004");
        Assert.Equal(5_000m, travelling.Amount);   // untouched

        var attendance = r.Lines.Single(l => l.Code == "A001");
        Assert.Equal(9_000m, attendance.Amount);   // 10,000 less its 1,000 share
    }

    [Fact]
    public void NoPay_beyond_a_full_month_is_capped_and_reported()
    {
        var r = PayrollCalculator.Calculate(Basic() with { NoPayDays = 45 });

        Assert.Equal(50_000m, r.NoPayDeduction);   // never more than the salary
        Assert.Equal(0m, r.EarnedBasic);
        Assert.Contains(r.Notes, n => n.Contains("exceeds a full month"));
    }

    // ── EPF and ETF ───────────────────────────────────────────────────────────

    [Fact]
    public void Epf_is_charged_on_the_liable_subtotal_not_on_gross()
    {
        var r = PayrollCalculator.Calculate(Basic() with
        {
            Components =
            [
                new() { Code = "A", Name = "EPF-liable allowance",  Amount = 10_000m, IsEpfLiable = true },
                new() { Code = "B", Name = "Outside EPF allowance", Amount = 20_000m, IsEpfLiable = false }
            ]
        });

        Assert.Equal(80_000m, r.GrossPay);          // everything
        Assert.Equal(60_000m, r.EpfLiableEarnings); // basic + the flagged one only

        Assert.Equal(4_800m, r.EmployeeEpf);   // 8%  of 60,000
        Assert.Equal(7_200m, r.EmployerEpf);   // 12% of 60,000
        Assert.Equal(1_800m, r.EmployerEtf);   // 3%  of 60,000
    }

    [Fact]
    public void Epf_follows_what_was_earned_after_no_pay()
    {
        var r = PayrollCalculator.Calculate(Basic() with { NoPayDays = 6 });

        Assert.Equal(40_000m, r.EpfLiableEarnings);   // 50,000 less 10,000 of no-pay
        Assert.Equal(3_200m, r.EmployeeEpf);
    }

    [Fact]
    public void A_non_member_contributes_nothing()
    {
        var r = PayrollCalculator.Calculate(Basic() with { IsEpfMember = false, IsEtfMember = false });

        Assert.Equal(0m, r.EmployeeEpf);
        Assert.Equal(0m, r.EmployerEpf);
        Assert.Equal(0m, r.EmployerEtf);
    }

    // ── Who pays what ─────────────────────────────────────────────────────────
    //
    // The rule these guard is statutory, not stylistic: ETF is wholly an employer
    // contribution and may never be recovered from the employee. Employer EPF likewise.
    // The calculator gets this right today by construction — TotalDeductions simply does not
    // mention them — but "right by construction" is one careless refactor from wrong, and
    // this is the kind of wrong that ends in a Labour Department complaint rather than a bug
    // report. Hence tests named after the rule.

    [Fact]
    public void Etf_is_never_deducted_from_the_employee()
    {
        var withEtf = PayrollCalculator.Calculate(Basic(100_000m) with { IsEtfMember = true });
        var without = PayrollCalculator.Calculate(Basic(100_000m) with { IsEtfMember = false });

        Assert.Equal(3_000m, withEtf.EmployerEtf);
        Assert.Equal(0m, without.EmployerEtf);

        // Whether ETF applies changes what the employer owes and nothing the employee sees.
        Assert.Equal(without.NetPay, withEtf.NetPay);
        Assert.Equal(without.TotalDeductions, withEtf.TotalDeductions);
    }

    [Fact]
    public void The_employers_epf_share_is_never_deducted_from_the_employee_either()
    {
        var normal = PayrollCalculator.Calculate(Basic(100_000m));
        var generous = PayrollCalculator.Calculate(Basic(100_000m) with { EmployerEpfPercent = 20m });

        Assert.Equal(12_000m, normal.EmployerEpf);
        Assert.Equal(20_000m, generous.EmployerEpf);

        // Raising the employer's share costs the employer, not the employee.
        Assert.Equal(normal.NetPay, generous.NetPay);
        Assert.Equal(normal.EmployeeEpf, generous.EmployeeEpf);
    }

    [Fact]
    public void Employer_contributions_show_up_only_in_cost_to_company()
    {
        var r = PayrollCalculator.Calculate(Basic(100_000m));

        // Deductions account for exactly the employee's own items and nothing else.
        Assert.Equal(r.EmployeeEpf + r.Apit + r.StampDuty + r.SrLevy
                     + r.TotalLoanInstalments + r.TotalOtherDeductions + r.BroughtForward,
                     r.TotalDeductions);

        // The employer's share is the whole of the difference between pay and cost.
        Assert.Equal(r.EmployerEpf + r.EmployerEtf, r.CostToCompany - r.GrossPay);
    }

    [Fact]
    public void No_pay_reduces_what_was_earned_rather_than_being_deducted_from_it()
    {
        // The distinction matters even though both routes reach the same net. Paying a full
        // basic and then deducting no-pay would leave EPF and tax charged on money the
        // employee never earned — the employee over-contributes and is over-taxed.
        var r = PayrollCalculator.Calculate(Basic(50_000m) with { NoPayDays = 6 });

        Assert.Equal(40_000m, r.GrossPay);              // gross itself is lower
        Assert.Equal(40_000m, r.EpfLiableEarnings);     // EPF follows earned pay
        Assert.Equal(40_000m, r.ApitLiableEarnings);    // so does tax
        Assert.Equal(3_200m, r.EmployeeEpf);            // 8% of 40,000, not of 50,000

        // And it is not also sitting in deductions, which would charge it twice.
        Assert.Equal(0m, r.TotalOtherDeductions);
    }

    [Fact]
    public void A_salary_advance_is_an_ordinary_employee_deduction()
    {
        var r = PayrollCalculator.Calculate(Basic(50_000m) with
        {
            IsEpfMember = false, IsEtfMember = false,
            Components = [new() { Code = "D0001", Name = "Salary Advance", Amount = 8_000m,
                                  Type = SalaryComponentType.Deduction }]
        });

        Assert.Equal(50_000m, r.GrossPay);              // recovering an advance is not a pay cut
        Assert.Equal(8_000m, r.TotalOtherDeductions);
        Assert.Equal(42_000m, r.NetPay);
    }

    // ── Tax ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(100_000, 0)]        // top of the free band
    [InlineData(120_000, 1_200)]    // 120,000 × 6%  − 6,000
    [InlineData(150_000, 3_500)]    // 150,000 × 12% − 14,500
    [InlineData(400_000, 70_500)]   // 400,000 × 36% − 73,500
    public void Tax_is_rate_times_earnings_less_relief(decimal earnings, decimal expected)
    {
        Assert.Equal(expected, PayrollCalculator.TaxFor(earnings, Bands));
    }

    [Fact]
    public void Tax_uses_one_band_not_a_cumulative_walk_up_the_brackets()
    {
        // The relief is what makes the figure continuous at a threshold. Accumulating across
        // bands as well would tax the lower slices twice — this catches that.
        var atThreshold = PayrollCalculator.TaxFor(141_667m, Bands);
        var justOver    = PayrollCalculator.TaxFor(141_668m, Bands);

        Assert.Equal(2_500.02m, atThreshold);
        Assert.Equal(2_500.16m, justOver);
        Assert.True(justOver - atThreshold < 1m, "tax must not jump at a band boundary");
    }

    [Fact]
    public void Tax_is_not_reduced_by_the_employees_own_epf()
    {
        // Sri Lankan APIT is charged on remuneration; the employee's EPF is not deductible
        // from it. Netting it off first is the commonest and most expensive mistake here.
        var r = PayrollCalculator.Calculate(Basic(150_000m));

        Assert.Equal(150_000m, r.ApitLiableEarnings);
        Assert.Equal(3_500m, r.Apit);              // on 150,000, not on 138,000
    }

    [Fact]
    public void Only_flagged_earnings_are_taxable()
    {
        var r = PayrollCalculator.Calculate(Basic(90_000m) with
        {
            Components =
            [
                new() { Code = "T", Name = "Taxable",  Amount = 30_000m, IsApitLiable = true },
                new() { Code = "N", Name = "Exempt",   Amount = 40_000m, IsApitLiable = false }
            ]
        });

        Assert.Equal(160_000m, r.GrossPay);
        Assert.Equal(120_000m, r.ApitLiableEarnings);
        Assert.Equal(1_200m, r.Apit);
    }

    [Fact]
    public void No_tax_table_deducts_nothing_and_says_so()
    {
        var r = PayrollCalculator.Calculate(Basic(500_000m) with { TaxBands = [] });

        Assert.Equal(0m, r.Apit);
        Assert.Contains(r.Notes, n => n.Contains("no tax table"));
    }

    // ── Carry forward ─────────────────────────────────────────────────────────

    [Fact]
    public void Deductions_beyond_pay_carry_forward_and_nothing_is_paid()
    {
        var r = PayrollCalculator.Calculate(Basic(20_000m) with
        {
            IsEpfMember = false, IsEtfMember = false,
            Loans = [new() { LoanId = 1, Code = "L1", Name = "Staff Loan", Instalment = 25_000m }]
        });

        Assert.Equal(0m, r.NetPay);
        Assert.Equal(5_000m, r.CarriedForward);
        Assert.Contains(r.Notes, n => n.Contains("carried forward"));
    }

    [Fact]
    public void Brought_forward_is_recovered_before_anything_else_carries_on()
    {
        var r = PayrollCalculator.Calculate(Basic(20_000m) with
        {
            IsEpfMember = false, IsEtfMember = false,
            BroughtForward = 5_000m
        });

        Assert.Equal(5_000m, r.BroughtForward);
        Assert.Equal(15_000m, r.NetPay);
        Assert.Equal(0m, r.CarriedForward);
    }

    [Fact]
    public void Carry_forward_can_be_switched_off_so_a_shortfall_is_not_double_recovered()
    {
        var r = PayrollCalculator.Calculate(Basic(20_000m) with
        {
            IsEpfMember = false, IsEtfMember = false,
            CarryForwardMinusSalary = false,
            Loans = [new() { LoanId = 1, Code = "L1", Name = "Staff Loan", Instalment = 25_000m }]
        });

        Assert.Equal(0m, r.CarriedForward);
        Assert.True(r.NetPay < 0m);
        Assert.Contains(r.Notes, n => n.Contains("carry-forward is off"));
    }

    // ── Levies ────────────────────────────────────────────────────────────────

    [Fact]
    public void Stamp_duty_is_not_charged_when_there_is_no_pay_to_charge_it_on()
    {
        // A whole month of absence must not create a debt out of an absence.
        var r = PayrollCalculator.Calculate(Basic() with { NoPayDays = 30, StampDuty = 25m });

        Assert.Equal(0m, r.GrossPay);
        Assert.Equal(0m, r.StampDuty);
    }

    [Fact]
    public void Stamp_duty_and_levy_are_deducted_when_there_is_pay()
    {
        var r = PayrollCalculator.Calculate(Basic(100_000m) with
        {
            StampDuty = 25m, SrLevyPercent = 2.5m
        });

        Assert.Equal(25m, r.StampDuty);
        Assert.Equal(2_500m, r.SrLevy);
    }

    // ── Arrears ───────────────────────────────────────────────────────────────

    [Fact]
    public void Arrears_are_taxable_and_epf_liable_but_kept_off_this_months_basic()
    {
        var r = PayrollCalculator.Calculate(Basic() with { SalaryArrears = 12_000m });

        Assert.Equal(50_000m, r.BasicSalary);              // unchanged
        Assert.Equal(62_000m, r.GrossPay);
        Assert.Equal(62_000m, r.EpfLiableEarnings);
        Assert.Equal(62_000m, r.ApitLiableEarnings);
        Assert.Contains(r.Lines, l => l.Code == "1118" && l.Amount == 12_000m);
    }

    [Fact]
    public void Arrears_can_be_placed_outside_epf_when_a_site_says_so()
    {
        var r = PayrollCalculator.Calculate(Basic() with
        {
            SalaryArrears = 12_000m, ArrearsAreEpfLiable = false
        });

        Assert.Equal(50_000m, r.EpfLiableEarnings);
        Assert.Equal(62_000m, r.ApitLiableEarnings);
    }

    // ── The payslip must add up ───────────────────────────────────────────────

    [Fact]
    public void Gross_less_deductions_equals_net_plus_what_was_carried()
    {
        // The single property every payslip must satisfy. If this fails, the payslip shows
        // figures that do not reconcile and nobody can tell where the money went.
        var r = PayrollCalculator.Calculate(Basic(120_000m) with
        {
            NoPayDays = 2,
            OvertimeAmount = 8_000m,
            SalaryArrears = 5_000m,
            StampDuty = 25m,
            SrLevyPercent = 1m,
            BroughtForward = 1_500m,
            Components =
            [
                new() { Code = "A001", Name = "Attendance", Amount = 10_000m,
                        IsEpfLiable = true, IsApitLiable = true, IncludeInNoPay = true },
                new() { Code = "D001", Name = "Welfare", Amount = 500m,
                        Type = SalaryComponentType.Deduction }
            ],
            Loans = [new() { LoanId = 1, Code = "L1", Name = "Staff Loan", Instalment = 3_000m }]
        });

        Assert.Equal(r.GrossPay - r.TotalDeductions, r.NetPay - r.CarriedForward);
    }

    [Fact]
    public void Total_deductions_is_the_sum_of_its_parts()
    {
        var r = PayrollCalculator.Calculate(Basic(120_000m) with
        {
            StampDuty = 25m,
            BroughtForward = 1_000m,
            Components = [new() { Code = "D", Name = "Welfare", Amount = 500m,
                                  Type = SalaryComponentType.Deduction }],
            Loans = [new() { LoanId = 1, Code = "L", Name = "Loan", Instalment = 2_000m }]
        });

        Assert.Equal(
            r.EmployeeEpf + r.Apit + r.StampDuty + r.SrLevy
            + r.TotalLoanInstalments + r.TotalOtherDeductions + r.BroughtForward,
            r.TotalDeductions);
    }

    [Fact]
    public void Cost_to_company_is_gross_plus_what_the_employer_contributes()
    {
        var r = PayrollCalculator.Calculate(Basic(100_000m));

        Assert.Equal(r.GrossPay + r.EmployerEpf + r.EmployerEtf, r.CostToCompany);
        Assert.Equal(115_000m, r.CostToCompany);   // 100,000 + 12,000 + 3,000
    }

    // ── Rounding ──────────────────────────────────────────────────────────────

    [Fact]
    public void Rounding_the_net_carries_the_difference_rather_than_absorbing_it()
    {
        // A year of rounding up would otherwise quietly cost the company a day's pay per
        // employee, with nothing recording where it went.
        var r = PayrollCalculator.Calculate(Basic(50_000m) with
        {
            IsEpfMember = false, IsEtfMember = false,
            StampDuty = 25.40m,
            RoundOffNetPay = true, RoundNearest = 1m
        });

        Assert.Equal(Math.Round(r.NetPay, 0), r.NetPay);
        Assert.NotEqual(0m, r.CarriedForward);
    }

    [Fact]
    public void Epf_rounding_mode_is_respected()
    {
        var r = PayrollCalculator.Calculate(Basic(48_333.33m) with
        {
            EpfRounding = RoundingMode.RoundOff
        });

        // 8% of 48,333.33 = 3,866.6664 → 3,867
        Assert.Equal(3_867m, r.EmployeeEpf);
        Assert.Equal(Math.Round(r.EmployeeEpf, 0), r.EmployeeEpf);
    }
}

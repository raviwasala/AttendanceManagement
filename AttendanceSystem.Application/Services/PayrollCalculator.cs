using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Inputs
//
// Plain records with no database types, so the whole calculation can be exercised
// from a test with figures written by hand. That is the point of this file: a
// payslip is the one output nobody reviews before it is believed, so the maths has
// to be checkable without standing up a database and a month of attendance.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>One earning or deduction going into the run, with the flags that decide its fate.</summary>
public sealed record PayComponentInput
{
    public int? SalaryComponentId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public SalaryComponentType Type { get; init; } = SalaryComponentType.Earning;

    public decimal Amount { get; init; }

    /// <summary>Counts toward the EPF-liable total.</summary>
    public bool IsEpfLiable { get; init; }

    /// <summary>Counts toward the APIT-liable total.</summary>
    public bool IsApitLiable { get; init; }

    /// <summary>Counts toward gross pay. An earning outside gross is paid but not reported in it.</summary>
    public bool IncludeInGrossPay { get; init; } = true;

    /// <summary>Reduced pro-rata by no-pay days, as basic is.</summary>
    public bool IncludeInNoPay { get; init; }

    /// <summary>True for a one-off; false for a standing item. Carried through to the payslip line.</summary>
    public bool IsOneOff { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>One tax band. Tax = earnings × Rate − Relief, which is how the IRD tables are written.</summary>
public sealed record TaxBandInput
{
    public decimal FromAmount { get; init; }

    /// <summary>Null is the open-ended top band.</summary>
    public decimal? ToAmount { get; init; }

    public decimal RatePercent { get; init; }
    public decimal Relief { get; init; }
}

/// <summary>A loan instalment due this month, with its balance so the payslip can show it.</summary>
public sealed record LoanInstalmentInput
{
    public int LoanId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public decimal Instalment { get; init; }
    public decimal InterestPortion { get; init; }

    /// <summary>Outstanding before this instalment.</summary>
    public decimal OpeningBalance { get; init; }
}

public sealed record PayrollInput
{
    public int Year { get; init; }
    public int Month { get; init; }

    // ── Attendance ────────────────────────────────────────────────────────────
    public decimal WorkingDays { get; init; }
    public decimal PresentDays { get; init; }
    public decimal LeaveDays { get; init; }
    public decimal NoPayDays { get; init; }
    public decimal OvertimeHours { get; init; }

    // ── Money in ──────────────────────────────────────────────────────────────
    public decimal BasicSalary { get; init; }
    public decimal OvertimeAmount { get; init; }
    public IReadOnlyList<PayComponentInput> Components { get; init; } = [];

    /// <summary>
    /// Back-pay owed because an increment took effect in a month already paid. Kept apart
    /// from the ordinary earnings because it is EPF-liable and taxable but must not be
    /// mistaken for this month's salary when anybody reads the payslip back.
    /// </summary>
    public decimal SalaryArrears { get; init; }
    public bool ArrearsAreEpfLiable { get; init; } = true;

    // ── Statutory ─────────────────────────────────────────────────────────────
    public bool IsEpfMember { get; init; } = true;
    public bool IsEtfMember { get; init; } = true;
    public bool IsApitApplicable { get; init; } = true;

    public decimal EmployeeEpfPercent { get; init; } = 8m;
    public decimal EmployerEpfPercent { get; init; } = 12m;
    public decimal EmployerEtfPercent { get; init; } = 3m;

    public IReadOnlyList<TaxBandInput> TaxBands { get; init; } = [];

    /// <summary>Extra tax the employee has asked to be deducted, on top of the table.</summary>
    public decimal AdditionalTaxAmount { get; init; }

    // ── Levies: both inert, deliberately ──────────────────────────────────────
    //
    // Stamp duty on salary receipts and the Social Responsibility Levy were both abolished
    // in Sri Lanka, so nothing configures these and they are always zero in the current
    // system. They are kept as parameters, and only as parameters, for two reasons: the
    // legacy paycode table carries STMP, BSTP, TXON and BSRL, so a historic month imported
    // from it can still be reproduced; and payroll levies here have been introduced and
    // withdrawn more than once.
    //
    // If a levy is reintroduced, add a versioned rate with an effective date — do NOT put a
    // fixed figure in configuration, because a reprinted payslip has to show the rate that
    // was actually applied at the time, not the rate in force today.

    /// <summary>Flat amount, charged only when there is pay to charge it on. Currently always 0.</summary>
    public decimal StampDuty { get; init; }

    /// <summary>
    /// Percentage of gross. Currently always 0.
    ///
    /// Note for anyone reviving this: the SRL was charged on the TAX payable, not on gross —
    /// the legacy table pairs "SRL on Bonus" with "Tax on Bonus". Reusing this field as-is
    /// would compute a levy roughly an order of magnitude too large.
    /// </summary>
    public decimal SrLevyPercent { get; init; }

    // ── Money out ─────────────────────────────────────────────────────────────
    public IReadOnlyList<LoanInstalmentInput> Loans { get; init; } = [];

    /// <summary>
    /// Shortfall carried from last month, when deductions exceeded pay. Positive means the
    /// employee owes it, and it is recovered here before anything is carried on again.
    /// </summary>
    public decimal BroughtForward { get; init; }

    /// <summary>
    /// When false, a negative net is left negative rather than carried. Some sites recover
    /// the shortfall in cash instead, and silently carrying it would double-recover.
    /// </summary>
    public bool CarryForwardMinusSalary { get; init; } = true;

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>
    /// The divisor for a day's pay. Thirty rather than the actual days in the month, unless a
    /// site says otherwise: dividing by the calendar length would pay a February absence more
    /// than a March one for the same salary.
    /// </summary>
    public int DaysPerMonth { get; init; } = 30;

    public RoundingMode EpfRounding { get; init; } = RoundingMode.Decimal;
    public RoundingMode EtfRounding { get; init; } = RoundingMode.Decimal;
    public RoundingMode NoPayRounding { get; init; } = RoundingMode.Decimal;

    public bool RoundOffNetPay { get; init; }
    public decimal RoundNearest { get; init; } = 1m;

    public bool IsBankTransfer { get; init; } = true;
}

// ─────────────────────────────────────────────────────────────────────────────
// Output
// ─────────────────────────────────────────────────────────────────────────────

public sealed record PayLine(
    int? SalaryComponentId, string Code, string Name,
    SalaryComponentType Type, decimal Amount, bool IsOneOff, bool IsEpfLiable, int SortOrder);

public sealed record PayrollResult
{
    public decimal BasicSalary { get; init; }
    public decimal NoPayDeduction { get; init; }
    public decimal EarnedBasic { get; init; }

    public decimal TotalFixedAllowances { get; init; }
    public decimal TotalVariableAllowances { get; init; }
    public decimal OvertimeAmount { get; init; }
    public decimal SalaryArrears { get; init; }
    public decimal GrossPay { get; init; }

    public decimal EpfLiableEarnings { get; init; }
    public decimal EmployeeEpf { get; init; }
    public decimal EmployerEpf { get; init; }
    public decimal EmployerEtf { get; init; }

    public decimal ApitLiableEarnings { get; init; }
    public decimal Apit { get; init; }

    public decimal StampDuty { get; init; }
    public decimal SrLevy { get; init; }

    public decimal TotalLoanInstalments { get; init; }
    public decimal TotalOtherDeductions { get; init; }
    public decimal BroughtForward { get; init; }
    public decimal TotalDeductions { get; init; }

    public decimal NetPay { get; init; }

    /// <summary>Shortfall passed to next month. Zero unless deductions exceeded pay.</summary>
    public decimal CarriedForward { get; init; }

    public decimal CostToCompany { get; init; }

    public bool IsBankTransfer { get; init; }

    public IReadOnlyList<PayLine> Lines { get; init; } = [];

    /// <summary>
    /// Anything the figures cannot say on their own — a net of zero because everything was
    /// carried, a tax table that covered nothing. Surfaced on the register so it is seen
    /// before the money moves rather than queried afterwards.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>
/// Turns one employee's month into one payslip.
///
/// Pure and static, like <see cref="AttendanceCalculator"/> and for the same reason: this is
/// the only place the money is decided, so it must be possible to check it with arithmetic
/// on paper. It touches no database, no clock and no session — everything it needs arrives
/// in <see cref="PayrollInput"/>.
///
/// The order below is not arbitrary and must not be rearranged casually:
///
///   1. no-pay reduces basic and the allowances flagged for it
///   2. gross is what is left, plus OT and arrears
///   3. EPF is on its own liable subtotal, NOT on gross
///   4. tax is on its own liable subtotal, NOT on gross and NOT after EPF
///   5. deductions come off, oldest debt (B/F) first
///   6. what cannot be recovered carries forward
///
/// Steps 3 and 4 are the ones people get wrong. EPF-liable and tax-liable are different
/// subtotals with different memberships, and neither equals gross.
/// </summary>
public static class PayrollCalculator
{
    public static PayrollResult Calculate(PayrollInput input)
    {
        var notes = new List<string>();
        var lines = new List<PayLine>();

        // ── 1. No pay ─────────────────────────────────────────────────────────
        //
        // A day costs basic plus the allowances that follow attendance, divided by the
        // month's day count. Allowances NOT flagged for no-pay are paid in full however
        // many days were worked — a travelling allowance settled against real journeys
        // should not also be cut for absence, or the absence is charged twice.

        var noPayBase = input.BasicSalary
                      + input.Components
                             .Where(c => c.Type == SalaryComponentType.Earning && c.IncludeInNoPay)
                             .Sum(c => c.Amount);

        var noPay = input.NoPayDays > 0 && input.DaysPerMonth > 0
            ? Round(noPayBase / input.DaysPerMonth * input.NoPayDays, input.NoPayRounding)
            : 0m;

        // Guards against a data error paying a negative salary: 45 no-pay days in a 30-day
        // month is a fault somewhere upstream, and it should not become a negative basic.
        if (noPay > noPayBase)
        {
            noPay = noPayBase;
            notes.Add($"No-pay of {input.NoPayDays:0.##} day(s) exceeds a full month; "
                    + "capped at the full salary. Check the attendance for this employee.");
        }

        var earnedBasic = Money(input.BasicSalary - Proportion(noPay, noPayBase, input.BasicSalary));

        if (input.BasicSalary > 0)
        {
            lines.Add(new PayLine(null, "B0001", "Basic Salary",
                SalaryComponentType.Earning, earnedBasic, false, true, 0));
        }

        // ── 2. Earnings ───────────────────────────────────────────────────────

        decimal fixedAllowances = 0m, variableAllowances = 0m;
        decimal epfLiable = 0m, apitLiable = 0m;
        decimal otherDeductions = 0m;

        // Basic is EPF-liable and taxable after no-pay, not before. Contributions follow
        // what was earned, not what would have been earned had nobody been absent.
        epfLiable += earnedBasic;
        apitLiable += earnedBasic;

        foreach (var c in input.Components.OrderBy(c => c.SortOrder).ThenBy(c => c.Code))
        {
            if (c.Type == SalaryComponentType.Deduction)
            {
                otherDeductions += c.Amount;
                lines.Add(new PayLine(c.SalaryComponentId, c.Code, c.Name,
                    SalaryComponentType.Deduction, c.Amount, c.IsOneOff, false, c.SortOrder));
                continue;
            }

            var amount = c.IncludeInNoPay
                ? Money(c.Amount - Proportion(noPay, noPayBase, c.Amount))
                : c.Amount;

            if (amount == 0m && c.Amount == 0m) continue;

            if (c.IsOneOff) variableAllowances += amount; else fixedAllowances += amount;
            if (c.IsEpfLiable) epfLiable += amount;
            if (c.IsApitLiable) apitLiable += amount;

            lines.Add(new PayLine(c.SalaryComponentId, c.Code, c.Name,
                SalaryComponentType.Earning, amount, c.IsOneOff, c.IsEpfLiable, c.SortOrder));
        }

        // Overtime is taxable but outside EPF unless a site says otherwise — and that choice
        // belongs to the OT rule, which has already decided it by the time the amount arrives
        // here. Treated as taxable pay; the caller adds it to epfLiable via a component if
        // its rule says EPF-liable.
        if (input.OvertimeAmount != 0m)
        {
            apitLiable += input.OvertimeAmount;
            lines.Add(new PayLine(null, "2020", "Overtime",
                SalaryComponentType.Earning, input.OvertimeAmount, true, false, 900));
        }

        if (input.SalaryArrears != 0m)
        {
            apitLiable += input.SalaryArrears;
            if (input.ArrearsAreEpfLiable) epfLiable += input.SalaryArrears;

            lines.Add(new PayLine(null, "1118", "Salary Arrears",
                SalaryComponentType.Earning, input.SalaryArrears, true,
                input.ArrearsAreEpfLiable, 901));
        }

        var gross = Money(earnedBasic
                        + input.Components
                               .Where(c => c.Type == SalaryComponentType.Earning && c.IncludeInGrossPay)
                               .Sum(c => c.IncludeInNoPay
                                    ? Money(c.Amount - Proportion(noPay, noPayBase, c.Amount))
                                    : c.Amount)
                        + input.OvertimeAmount
                        + input.SalaryArrears);

        // ── 3. EPF and ETF ────────────────────────────────────────────────────

        var employeeEpf = input.IsEpfMember
            ? Round(epfLiable * input.EmployeeEpfPercent / 100m, input.EpfRounding) : 0m;
        var employerEpf = input.IsEpfMember
            ? Round(epfLiable * input.EmployerEpfPercent / 100m, input.EpfRounding) : 0m;
        var employerEtf = input.IsEtfMember
            ? Round(epfLiable * input.EmployerEtfPercent / 100m, input.EtfRounding) : 0m;

        // ── 4. APIT ───────────────────────────────────────────────────────────
        //
        // Deliberately NOT reduced by EPF. Sri Lankan APIT is charged on remuneration; the
        // employee's own EPF contribution is not deductible from it. Netting it off first is
        // a common and expensive mistake.

        var apit = 0m;
        if (input.IsApitApplicable && apitLiable > 0m)
        {
            if (input.TaxBands.Count == 0)
            {
                notes.Add("Taxable, but no tax table applies to this month — no APIT was deducted.");
            }
            else
            {
                apit = TaxFor(apitLiable, input.TaxBands);
                if (apit < 0m) apit = 0m;
            }

            apit = Money(apit + input.AdditionalTaxAmount);
        }

        // ── 5. Levies ─────────────────────────────────────────────────────────
        //
        // Charged only when there is pay to charge them on. Levying stamp duty against an
        // employee whose whole salary went to no-pay would create a debt out of an absence.

        var stampDuty = gross > 0m ? input.StampDuty : 0m;
        var srLevy = input.SrLevyPercent > 0m && gross > 0m
            ? Money(gross * input.SrLevyPercent / 100m) : 0m;

        // ── 6. Loans ──────────────────────────────────────────────────────────

        decimal loanTotal = 0m;
        foreach (var loan in input.Loans)
        {
            if (loan.Instalment <= 0m) continue;
            loanTotal += loan.Instalment;
            lines.Add(new PayLine(null, loan.Code, loan.Name,
                SalaryComponentType.Deduction, loan.Instalment, false, false, 950));
        }

        // ── 7. Net, and what cannot be paid ───────────────────────────────────

        var broughtForward = input.BroughtForward > 0m ? input.BroughtForward : 0m;

        var totalDeductions = Money(employeeEpf + apit + stampDuty + srLevy
                                  + loanTotal + otherDeductions + broughtForward);

        var net = Money(gross - totalDeductions);
        var carriedForward = 0m;

        if (net < 0m)
        {
            if (input.CarryForwardMinusSalary)
            {
                carriedForward = Money(-net);
                net = 0m;
                notes.Add($"Deductions exceeded pay by {carriedForward:N2}. "
                        + "Nothing is paid this month and the shortfall is carried forward.");
            }
            else
            {
                notes.Add($"Deductions exceed pay by {Math.Abs(net):N2} and carry-forward is off "
                        + "for this branch — recover it outside the payroll.");
            }
        }

        if (input.RoundOffNetPay && net > 0m && input.RoundNearest > 0m)
        {
            var rounded = Math.Round(net / input.RoundNearest, 0, MidpointRounding.AwayFromZero)
                        * input.RoundNearest;
            // The rounding difference is carried rather than absorbed, so a year of rounding
            // up does not quietly cost the company a day's pay per employee.
            carriedForward = Money(carriedForward + (rounded - net));
            net = rounded;
        }

        return new PayrollResult
        {
            BasicSalary = input.BasicSalary,
            NoPayDeduction = noPay,
            EarnedBasic = earnedBasic,
            TotalFixedAllowances = Money(fixedAllowances),
            TotalVariableAllowances = Money(variableAllowances),
            OvertimeAmount = input.OvertimeAmount,
            SalaryArrears = input.SalaryArrears,
            GrossPay = gross,

            EpfLiableEarnings = Money(epfLiable),
            EmployeeEpf = employeeEpf,
            EmployerEpf = employerEpf,
            EmployerEtf = employerEtf,

            ApitLiableEarnings = Money(apitLiable),
            Apit = apit,

            StampDuty = stampDuty,
            SrLevy = srLevy,

            TotalLoanInstalments = Money(loanTotal),
            TotalOtherDeductions = Money(otherDeductions),
            BroughtForward = broughtForward,
            TotalDeductions = totalDeductions,

            NetPay = net,
            CarriedForward = carriedForward,

            // What the employee costs, not what they receive: the employer's contributions
            // never appear on the payslip but are the difference between a salary and a
            // headcount budget.
            CostToCompany = Money(gross + employerEpf + employerEtf),

            IsBankTransfer = input.IsBankTransfer,
            Lines = lines,
            Notes = notes
        };
    }

    /// <summary>
    /// Tax from the band the earnings fall in: earnings × rate − relief.
    ///
    /// A single band, not a cumulative walk up the brackets. That is how the IRD monthly
    /// tables are written — the relief is what makes the figure continuous at each threshold,
    /// so accumulating across bands as well would tax the lower slices twice.
    /// </summary>
    public static decimal TaxFor(decimal earnings, IReadOnlyList<TaxBandInput> bands)
    {
        var band = bands
            .OrderBy(b => b.FromAmount)
            .LastOrDefault(b => earnings >= b.FromAmount
                             && (b.ToAmount == null || earnings <= b.ToAmount));

        if (band == null) return 0m;

        return Money(earnings * band.RatePercent / 100m - band.Relief);
    }

    /// <summary>
    /// The share of a total reduction belonging to one part of its base.
    ///
    /// No-pay is worked out once against the whole base and then split, rather than computed
    /// per component. Rounding each component separately and adding them up gives a different
    /// figure from the one shown as "No Pay" on the payslip, and the payslip would not add up.
    /// </summary>
    private static decimal Proportion(decimal reduction, decimal wholeBase, decimal part)
    {
        if (reduction == 0m || wholeBase <= 0m || part == 0m) return 0m;
        return Money(reduction * (part / wholeBase));
    }

    private static decimal Round(decimal value, RoundingMode mode) => mode switch
    {
        RoundingMode.RoundOff  => Math.Round(value, 0, MidpointRounding.AwayFromZero),
        RoundingMode.Nearest10 => Math.Round(value / 10m, 0, MidpointRounding.AwayFromZero) * 10m,
        _ => Money(value)
    };

    /// <summary>Two decimal places, away from zero — the convention every figure here uses.</summary>
    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

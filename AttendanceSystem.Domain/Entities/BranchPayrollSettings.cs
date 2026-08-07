using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Payroll parameters for one branch.
///
/// Per branch rather than per company because the things on it already are: EPF and ETF are
/// registered per branch, returns are filed against those registrations, and salaries are
/// commonly paid from a branch's own account. A single company-wide row would produce returns
/// that cannot be submitted and a transfer file drawn on the wrong account.
///
/// The percentages here are nullable on purpose — null means "use the company rate in force"
/// from <see cref="EpfEtfRate"/>. Copying 8/12/3 into every branch would look equivalent and
/// then silently freeze them all the next time the statutory rate moved.
/// </summary>
public class BranchPayrollSettings : BaseEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // ── Statutory registration ────────────────────────────────────────────────

    /// <summary>
    /// The EPF zone or district code printed alongside the registration number on the return.
    /// Two characters in practice, but held as text — it is an identifier, not a number.
    /// </summary>
    public string? EpfDCode { get; set; }

    /// <summary>Person named on the EPF return, and their number, for the fund to contact.</summary>
    public string? EpfContactPerson { get; set; }
    public string? EpfContactPhone { get; set; }

    public string? PayeRegistrationNo { get; set; }

    /// <summary>
    /// Years a non-citizen employee is taxed under the concessionary treatment before moving
    /// to the ordinary table.
    /// </summary>
    public int NonCitizenTaxYears { get; set; } = 4;

    // ── Contribution rates ────────────────────────────────────────────────────
    // Null means the company rate applies. Set only where this branch genuinely differs.

    public decimal? EmployeeEpfPercent { get; set; }
    public decimal? EmployerEpfPercent { get; set; }
    public decimal? EmployerEtfPercent { get; set; }

    // ── The month ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Days a monthly salary is divided by to get a daily rate — for no-pay and part-months.
    /// Thirty is the common convention; using the calendar length instead makes February
    /// dearer per day than January, which most sites do not intend.
    /// </summary>
    public int DaysPerMonth { get; set; } = 30;

    /// <summary>Hours in a working day, for deriving a daily rate from an hourly one.</summary>
    public decimal HoursPerDay { get; set; } = 8m;

    // ── Company bank account ──────────────────────────────────────────────────
    // Where salaries are paid *from*, as opposed to the employee accounts they go to.

    public int? BankBranchId { get; set; }
    public BankBranch? BankBranch { get; set; }
    public string? AccountNumber { get; set; }

    // ── Gratuity ──────────────────────────────────────────────────────────────

    /// <summary>Percentage of basic salary earned per qualifying year of service.</summary>
    public decimal GratuityPercentOfBasic { get; set; } = 50m;

    /// <summary>Years of service before any gratuity is due. Five is the statutory minimum.</summary>
    public int GratuityQualifyingYears { get; set; } = 5;

    // ── Net pay handling ──────────────────────────────────────────────────────

    /// <summary>Round the final payable figure, so the cash or transfer is a round number.</summary>
    public bool RoundOffSalaryPayable { get; set; }

    /// <summary>What to round the payable figure to when the above is on.</summary>
    public decimal RoundNearest { get; set; } = 1m;

    /// <summary>
    /// Carry a negative net pay forward to next month rather than demanding it back.
    ///
    /// Happens when deductions exceed earnings — a full month of no-pay against a standing
    /// loan instalment, for instance. Without this the payslip shows a negative figure that
    /// no payment run can act on.
    /// </summary>
    public bool CarryForwardMinusSalary { get; set; } = true;

    /// <summary>
    /// Carry the rounding remainder forward, so rounding does not quietly gain or lose money
    /// over the year.
    /// </summary>
    public bool CarryForwardCoins { get; set; } = true;

    // ── Rounding, per figure ──────────────────────────────────────────────────

    public RoundingMode EpfRounding { get; set; } = RoundingMode.Decimal;
    public RoundingMode EtfRounding { get; set; } = RoundingMode.Decimal;
    public RoundingMode NoPayRounding { get; set; } = RoundingMode.Decimal;
    public RoundingMode TaxRounding { get; set; } = RoundingMode.RoundOff;
    public RoundingMode LoanRounding { get; set; } = RoundingMode.Decimal;
    public RoundingMode OvertimeRounding { get; set; } = RoundingMode.Decimal;
}

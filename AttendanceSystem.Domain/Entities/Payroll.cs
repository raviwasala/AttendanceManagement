using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// One payroll run: a month, for a branch or for the whole company.
///
/// Per branch because EPF and ETF are filed against a branch's own employer registration —
/// a single company-wide run would produce returns that cannot be submitted.
/// </summary>
public class PayrollPeriod : BaseEntity
{
    public int Month { get; set; }
    public int Year { get; set; }

    /// <summary>Null runs every branch at once, for sites with a single registration.</summary>
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    /// <summary>
    /// Rates and tax table in force for this run, captured when it was processed.
    ///
    /// Recorded rather than looked up again at print time: a payslip reprinted after a budget
    /// change must show what was actually paid, not what the rules say today.
    /// </summary>
    public decimal EmployeeEpfPercent { get; set; }
    public decimal EmployerEpfPercent { get; set; }
    public decimal EmployerEtfPercent { get; set; }

    public DateTime? ProcessedAt { get; set; }
    public int? ProcessedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedBy { get; set; }

    public string? Notes { get; set; }

    public ICollection<Payslip> Payslips { get; set; } = new List<Payslip>();
}

/// <summary>
/// One employee's pay for one period — the payslip.
///
/// Every figure is stored rather than recomputed on display. A payslip is a statement of what
/// was paid: if reopening it re-ran the calculation, a later change to a grade or an allowance
/// would silently rewrite history, and the copy the employee holds would stop matching the
/// system.
/// </summary>
public class Payslip : BaseEntity
{
    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // ── Attendance inputs, copied from the attendance module ───────────────────

    public int WorkingDays { get; set; }
    public int PresentDays { get; set; }
    public int LeaveDays { get; set; }

    /// <summary>Days not paid, derived from attendance absences.</summary>
    public decimal NoPayDays { get; set; }

    public decimal OvertimeHours { get; set; }

    // ── Earnings ──────────────────────────────────────────────────────────────

    /// <summary>Grade basic, before any no-pay reduction.</summary>
    public decimal BasicSalary { get; set; }

    /// <summary>What the no-pay days cost, across basic and pro-rated allowances.</summary>
    public decimal NoPayDeduction { get; set; }

    public decimal TotalFixedAllowances { get; set; }
    public decimal TotalVariableAllowances { get; set; }
    public decimal OvertimeAmount { get; set; }

    public decimal GrossPay { get; set; }

    // ── Statutory ─────────────────────────────────────────────────────────────

    /// <summary>Earnings EPF is charged on — basic plus fixed allowances, less no-pay.</summary>
    public decimal EpfLiableEarnings { get; set; }

    public decimal EmployeeEpf { get; set; }
    public decimal EmployerEpf { get; set; }
    public decimal EmployerEtf { get; set; }

    public decimal ApitLiableEarnings { get; set; }
    public decimal Apit { get; set; }

    /// <summary>Basic after no-pay — what was actually earned, and what EPF is charged on.</summary>
    public decimal EarnedBasic { get; set; }

    /// <summary>Back-pay for a raise that started in a month already paid.</summary>
    public decimal SalaryArrears { get; set; }

    // ── Other deductions and the result ───────────────────────────────────────

    public decimal TotalLoanInstalments { get; set; }

    /// <summary>Historic levies, both abolished. Stored so an imported legacy month reconciles.</summary>
    public decimal StampDuty { get; set; }
    public decimal SrLevy { get; set; }

    /// <summary>
    /// Shortfall recovered from last month, when deductions had exceeded pay. Part of this
    /// month's deductions.
    /// </summary>
    public decimal BroughtForward { get; set; }

    public decimal TotalOtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    /// <summary>
    /// What could not be recovered this month and passes to the next. Read as next month's
    /// <see cref="BroughtForward"/>, which is why it is stored rather than recomputed —
    /// re-deriving it later would change a figure that has already been paid against.
    /// </summary>
    public decimal CarriedForward { get; set; }

    /// <summary>
    /// Bank transfer or cash, decided at run time from the employee's setup. Stored because
    /// the bank file and the cash list are both built from it, and somebody switching to
    /// bank next month must not move a past payslip out of the cash list.
    /// </summary>
    public bool IsBankTransfer { get; set; } = true;

    /// <summary>
    /// Anything the figures cannot say for themselves — a net of zero because everything was
    /// carried, a taxable employee with no tax table. Shown on the register so it is seen
    /// before the money moves.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Total cost to the employer — gross plus the employer's own EPF and ETF. Not deducted
    /// from anyone, but it is the number a manager means by "what does this person cost".
    /// </summary>
    public decimal CostToCompany { get; set; }

    // ── Snapshot of where the money went ──────────────────────────────────────
    // Copied rather than joined: an employee who changes bank after payday must not change
    // where a past payslip says they were paid.

    public string? BankName { get; set; }
    public string? BankBranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? EpfNumber { get; set; }

    public ICollection<PayslipLine> Lines { get; set; } = new List<PayslipLine>();
}

/// <summary>
/// One allowance or deduction line on a payslip, with the component's name copied in.
///
/// The name is stored, not joined, for the same reason as the bank details: renaming a
/// component must not restate what an old payslip said.
/// </summary>
public class PayslipLine : BaseEntity
{
    public int PayslipId { get; set; }
    public Payslip Payslip { get; set; } = null!;

    public int? SalaryComponentId { get; set; }
    public SalaryComponent? SalaryComponent { get; set; }

    public string ComponentName { get; set; } = string.Empty;
    public string? ComponentCode { get; set; }

    public SalaryComponentType ComponentType { get; set; }
    public decimal Amount { get; set; }

    public bool IsRecurring { get; set; }
    public bool IsEpfLiable { get; set; }
    public int SortOrder { get; set; }
}

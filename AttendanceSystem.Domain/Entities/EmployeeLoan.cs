using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A loan granted to an employee.
///
/// The rate and interest model are <b>copied from the loan type at grant time</b> rather than
/// read through it. Changing the type later must not restate what somebody already owes, and
/// a borrower who agreed to 6% is owed 6% whatever the type says next year.
/// </summary>
public class EmployeeLoan : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int LoanTypeId { get; set; }
    public LoanType LoanType { get; set; } = null!;

    /// <summary>When the loan was granted.</summary>
    public DateTime LoanDate { get; set; }

    /// <summary>The rate actually granted — a copy, not a reference.</summary>
    public decimal InterestRate { get; set; }

    /// <summary>Flat or reducing, as it stood when the loan was granted.</summary>
    public LoanInterestType InterestType { get; set; }

    /// <summary>Principal lent.</summary>
    public decimal LoanAmount { get; set; }

    /// <summary>Interest over the life of the loan, computed at grant time.</summary>
    public decimal InterestAmount { get; set; }

    /// <summary>Principal plus interest — what is repaid in total.</summary>
    public decimal TotalPayable { get; set; }

    public int NumberOfInstallments { get; set; }
    public decimal MonthlyInstallment { get; set; }

    /// <summary>
    /// Deduction starts in the month the loan was granted rather than the next one.
    ///
    /// The "Reduce this Month" decision, kept because it changes when the first instalment
    /// falls and therefore every date in the schedule.
    /// </summary>
    public bool ReduceThisMonth { get; set; } = true;

    /// <summary>The first month an instalment is deducted, derived when the loan is granted.</summary>
    public int FirstDeductionYear { get; set; }
    public int FirstDeductionMonth { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Active;

    /// <summary>
    /// Literal reading of the old system's checkbox on the guarantors tab. Recorded so the
    /// intent is not lost; confirm the meaning before anything relies on it.
    /// </summary>
    public bool AllowGuarantorsToGrantLoans { get; set; }

    public string? Notes { get; set; }

    public ICollection<LoanGuarantor> Guarantors { get; set; } = new List<LoanGuarantor>();
    public ICollection<LoanTransaction> Transactions { get; set; } = new List<LoanTransaction>();
}

/// <summary>
/// Somebody standing behind a loan. Up to four, as the old system allows.
///
/// A link to an employee rather than a typed name: a guarantor's exposure across several
/// loans is a question worth being able to ask, and free text could not answer it.
/// </summary>
public class LoanGuarantor : BaseEntity
{
    public int EmployeeLoanId { get; set; }
    public EmployeeLoan EmployeeLoan { get; set; } = null!;

    public int GuarantorEmployeeId { get; set; }
    public Employee GuarantorEmployee { get; set; } = null!;

    /// <summary>1 to 4 — which slot on the form, kept so the order is stable.</summary>
    public int Position { get; set; }
}

/// <summary>
/// Money coming off a loan: a payroll deduction, or a settlement paid directly.
///
/// The balance is derived from these rather than stored on the loan. A running total that is
/// written to would eventually disagree with the transactions behind it, and reconciling a
/// loan balance against nothing is impossible.
/// </summary>
public class LoanTransaction : BaseEntity
{
    public int EmployeeLoanId { get; set; }
    public EmployeeLoan EmployeeLoan { get; set; } = null!;

    public DateTime TransactionDate { get; set; }

    /// <summary>The payroll month this came off, when it was a deduction.</summary>
    public int? Year { get; set; }
    public int? Month { get; set; }

    public LoanTransactionType TransactionType { get; set; } = LoanTransactionType.Installment;

    public decimal Amount { get; set; }

    /// <summary>The run that recovered it. Null for a direct settlement.</summary>
    public int? PayrollPeriodId { get; set; }
    public PayrollPeriod? PayrollPeriod { get; set; }

    public string? Notes { get; set; }
}

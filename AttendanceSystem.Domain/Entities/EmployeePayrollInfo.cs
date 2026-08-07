namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// The payroll half of an employee record: statutory numbers, grade, and where the money goes.
///
/// Separate from <see cref="Employee"/> because the two are maintained by different people for
/// different reasons — HR keeps the personal record, payroll keeps this — and because an
/// employee can exist perfectly well before anyone has decided their grade or collected a bank
/// account. Folding these onto Employee would mean either nullable columns nobody trusts, or
/// blocking registration on payroll data.
/// </summary>
public class EmployeePayrollInfo : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// The member number under the employer's EPF registration.
    ///
    /// Not unique across the company: it is unique within a branch's employer registration,
    /// and a group with several registrations will legitimately repeat member numbers across
    /// them. The branch is what disambiguates.
    /// </summary>
    public string? EpfNumber { get; set; }

    public string? EtfNumber { get; set; }

    /// <summary>
    /// False for staff outside the scheme — most commonly contract or casual staff, and
    /// members past retirement age. Both funds are opt-outable independently.
    /// </summary>
    public bool IsEpfMember { get; set; } = true;
    public bool IsEtfMember { get; set; } = true;

    /// <summary>
    /// Whether APIT is deducted at source. Off for employees below the threshold or whose
    /// tax is settled elsewhere; the calculation still runs and simply deducts nothing, so
    /// the payslip shows the figure that would have applied.
    /// </summary>
    public bool IsApitApplicable { get; set; } = true;

    /// <summary>
    /// Which APIT table applies. Null falls back to the default table.
    ///
    /// Per employee because primary and secondary employment are taxed differently, and
    /// somebody with a second job is on a different table from a colleague on the same grade.
    /// </summary>
    public int? ApitTaxTableId { get; set; }
    public ApitTaxTable? ApitTaxTable { get; set; }

    /// <summary>
    /// The employer bears this employee's APIT rather than deducting it — the net is agreed
    /// and the company absorbs the tax.
    ///
    /// Changes the arithmetic rather than just who pays: grossing up raises taxable earnings,
    /// which raises the tax, which raises the gross again. The calculation converges rather
    /// than being a single multiplication.
    /// </summary>
    public bool IsTaxOnTax { get; set; }

    /// <summary>
    /// A fixed amount added to the computed APIT each month — used for arrears, or where the
    /// employee has asked for extra to be withheld.
    /// </summary>
    public decimal AdditionalTaxAmount { get; set; }

    /// <summary>
    /// Employment type. Drives eligibility — EPF membership, gratuity, leave — not pay.
    /// </summary>
    public int? EmploymentCategoryId { get; set; }
    public EmploymentCategory? EmploymentCategory { get; set; }

    /// <summary>
    /// Maximum overtime hours payable in a month for this employee. 0 means no cap.
    ///
    /// Held per employee rather than per shift because the cap is usually a term of the
    /// individual's contract, not a property of when they work.
    /// </summary>
    public decimal OtLimitHours { get; set; }

    /// <summary>
    /// The branch whose EPF/ETF employer registration this employee is filed under.
    ///
    /// Usually their working branch, but not always: staff can be seconded while remaining on
    /// another registration, and the return has to follow the registration rather than the
    /// desk. Null means the employee's own branch.
    /// </summary>
    public int? EpfRegistrationBranchId { get; set; }
    public Branch? EpfRegistrationBranch { get; set; }

    /// <summary>
    /// EPF status code as used on the statutory return — 'E' for an ordinary member and so on.
    /// Free text because the accepted codes are set by the fund, not by this system.
    /// </summary>
    public string? EpfStatus { get; set; }

    // ── Rate overrides ────────────────────────────────────────────────────────
    // Null means "use the company rate in force". Present because contributing above the
    // statutory minimum is common for directors and long-service staff, and the alternative
    // — a separate rate table per person — would obscure who is actually on the standard rate.

    public decimal? EmployeeEpfPercentOverride { get; set; }
    public decimal? EmployerEpfPercentOverride { get; set; }
    public decimal? EmployerEtfPercentOverride { get; set; }

    /// <summary>Basic salary normally comes from the grade — see <see cref="SalaryGrade"/>.</summary>
    public int? SalaryGradeId { get; set; }
    public SalaryGrade? SalaryGrade { get; set; }

    /// <summary>
    /// This employee's own basic salary, overriding the grade. Null means the grade applies.
    ///
    /// Nullable rather than a plain amount, and that distinction carries the whole design: a
    /// blank means "whatever the grade pays", so a company-wide grade revision still moves
    /// everyone who has not been singled out. Storing a copy of the grade's figure against
    /// every employee would silently freeze them all at today's rate.
    ///
    /// Where both exist the override wins — it is the more specific statement, and the
    /// payslip records which was used so the reason a figure differs is never a mystery.
    /// </summary>
    public decimal? BasicSalaryOverride { get; set; }

    public int? SalaryGroupId { get; set; }
    public SalaryGroup? SalaryGroup { get; set; }

    public int? SubDepartmentId { get; set; }
    public SubDepartment? SubDepartment { get; set; }

    // ── Bank ──────────────────────────────────────────────────────────────────

    public int? BankBranchId { get; set; }
    public BankBranch? BankBranch { get; set; }

    public string? AccountNumber { get; set; }

    /// <summary>
    /// Name as it appears on the bank account, when it differs from the employee's name.
    /// A transfer rejected for a name mismatch is the commonest payday failure.
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>Paid by bank transfer rather than cash. Cash staff are excluded from the SLIPS file.</summary>
    public bool IsBankTransfer { get; set; } = true;

    // ── Payroll suspension ────────────────────────────────────────────────────
    //
    // Still employed, temporarily not paid — unpaid leave, suspension, an overseas
    // posting. Distinct from resignation, which is an employee status and permanent, and
    // distinct from deleting the payroll record, which would lose the bank details they
    // will need again on their return.

    /// <summary>Excluded from payroll runs while true.</summary>
    public bool IsPayrollSuspended { get; set; }

    /// <summary>When the exclusion started, and when it is expected to end. Null end means open.</summary>
    public DateTime? SuspendedFrom { get; set; }
    public DateTime? SuspendedTo { get; set; }

    /// <summary>Why. Required when suspending, so a payroll clerk is never left guessing.</summary>
    public string? SuspendReason { get; set; }
}

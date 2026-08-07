namespace AttendanceSystem.Domain.Enums;

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3,
    HalfDay = 4,
    OnLeave = 5,
    Holiday = 6,
    WeeklyOff = 7
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum HolidayType
{
    /// <summary>Gazetted public holiday — applies to everyone.</summary>
    Public = 1,

    /// <summary>Declared by the company: a shutdown day, an anniversary.</summary>
    Company = 2,

    /// <summary>
    /// A one-off: a declared day of mourning, an election, a local event.
    ///
    /// Treated identically to the others by every calculation — the distinction is for
    /// reporting and for knowing which entries not to carry into next year. Stored as an
    /// int, so adding it needs no migration.
    /// </summary>
    Special = 3
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}

/// <summary>
/// Where an employee stands with the company.
///
/// Replaces the bare <c>IsActive</c> flag as the *reason* someone is not working, which that
/// boolean could not express: resigned, suspended and on long-term absence were all "inactive"
/// and indistinguishable. IsActive stays as the derived shorthand every existing query uses.
/// </summary>
public enum EmployeeStatus
{
    Active = 1,
    Resigned = 2,
    Terminated = 3,

    /// <summary>Temporarily not working, but still employed — the record is expected back.</summary>
    Suspended = 4,

    /// <summary>Long-term absence: extended medical leave, sabbatical.</summary>
    OnLongLeave = 5
}

/// <summary>What an <c>EmployeeHistory</c> entry records.</summary>
public enum EmployeeChangeType
{
    /// <summary>Moved department, designation or branch.</summary>
    Transfer = 1,

    /// <summary>Promotion or change of job title, without moving department.</summary>
    Promotion = 2,

    StatusChange = 3,
    Resignation = 4,

    /// <summary>Came back after leaving — keeps the original record rather than duplicating the person.</summary>
    Rejoin = 5
}

/// <summary>
/// How far a user's approval rights reach.
///
/// Separate from the <c>Leave.Approve</c> permission, which says *whether* somebody may
/// approve. This says *for whom* — a dimension the permission model has no way to express,
/// since a permission is a capability with no scope attached.
///
/// Stated explicitly on the user rather than inferred from whether they appear in any
/// department. Inferring it made company-wide approval invisible — you could only tell by
/// noticing an absence of rows — and meant naming somebody for one department silently
/// demoted them from approving everywhere.
/// </summary>
public enum ApprovalScope
{
    /// <summary>Approves for every department. The default, and what HR and administrators need.</summary>
    CompanyWide = 1,

    /// <summary>
    /// Approves only for departments they head or are named an approver of. A user set to
    /// this with no departments assigned approves nothing — which is a real state, not a
    /// mistake, and is reported as such rather than silently behaving like company-wide.
    /// </summary>
    AssignedDepartments = 2
}

public enum EmployeeDocumentType
{
    Nic = 1,
    Passport = 2,
    Certificate = 3,
    Contract = 4,
    Resume = 5,
    MedicalRecord = 6,
    Photo = 7,
    Other = 99
}

/// <summary>Reachability of a fingerprint device, derived from the last contact attempt.</summary>
public enum DeviceStatus
{
    /// <summary>Never contacted.</summary>
    Unknown = 0,
    Online = 1,
    Offline = 2,
    /// <summary>Repeated failures — needs attention rather than a retry.</summary>
    Error = 3
}

public enum SyncTrigger
{
    Manual = 1,
    Scheduled = 2
}

public enum SyncOutcome
{
    Success = 1,
    /// <summary>Punches downloaded, but some could not be mapped or processed.</summary>
    PartialSuccess = 2,
    Failed = 3
}

/// <summary>
/// Which kind of day an overtime rule applies to. Weekly off and holiday work usually
/// attract a higher multiplier than extra hours on an ordinary working day.
/// </summary>
public enum OvertimeDayType
{
    /// <summary>Matches any day — the fallback rule.</summary>
    Any = 0,
    WorkingDay = 1,
    WeeklyOff = 2,
    Holiday = 3
}

public enum OvertimeStatus
{
    /// <summary>Claimed from attendance, awaiting a decision.</summary>
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

// ── Payroll ────────────────────────────────────────────────────────────────────

public enum SalaryComponentType
{
    /// <summary>Adds to gross pay — allowances, incentives.</summary>
    Earning = 1,

    /// <summary>Subtracts from gross pay — loans, advances, union dues.
    /// EPF, ETF and APIT are not components: they are computed, not configured.</summary>
    Deduction = 2
}

public enum ComponentCalculationType
{
    /// <summary>A money amount.</summary>
    FixedAmount = 1,

    /// <summary>A percentage of the grade's basic salary.</summary>
    PercentOfBasic = 2
}

/// <summary>
/// Where a payroll run has got to.
///
/// Draft is recalculable; Approved is not. The split exists because the figures are checked
/// by a person between the two, and a run that silently recalculated after that check would
/// make the check meaningless.
/// </summary>
public enum PayrollStatus
{
    /// <summary>Calculated, still editable, safe to re-run.</summary>
    Draft = 1,

    /// <summary>Signed off. Figures are frozen; reopening is deliberate and recorded.</summary>
    Approved = 2,

    /// <summary>Paid out and filed. Terminal.</summary>
    Paid = 3
}

/// <summary>
/// Whether a salary component repeats every month or is entered for a single month.
///
/// Replaces an earlier "IsFixed" flag, which conflated this with EPF liability. They are
/// independent: a recurring allowance may be outside EPF, and a one-off payment may be
/// inside it.
/// </summary>
public enum ComponentRecurrence
{
    /// <summary>Paid every month as part of the package.</summary>
    Monthly = 1,

    /// <summary>Entered for one month only — a bonus, arrears, a one-time reimbursement.</summary>
    OneOff = 2
}

/// <summary>
/// How a loan's interest is worked out. The two produce very different totals for the same
/// headline rate, so which one a loan uses has to be recorded rather than assumed.
/// </summary>
public enum LoanInterestType
{
    /// <summary>
    /// Flat: interest is charged on the original principal for the whole term, regardless of
    /// how much has been repaid. Simple to compute and the dearer of the two for the borrower.
    /// </summary>
    Fixed = 1,

    /// <summary>
    /// Reducing balance: interest each period is charged on what is still outstanding, so it
    /// falls as the loan is repaid.
    /// </summary>
    Reducing = 2
}

/// <summary>
/// How a computed payroll figure is rounded before it reaches the payslip.
///
/// Per figure rather than one setting for all of them: sites commonly keep EPF and ETF to the
/// cent because the returns are reconciled to it, while rounding tax and net pay to whole
/// rupees. One global rule would force the same treatment on both.
/// </summary>
public enum RoundingMode
{
    /// <summary>Keep two decimal places — no rounding beyond normal currency precision.</summary>
    Decimal = 1,

    /// <summary>Round to the nearest whole rupee.</summary>
    RoundOff = 2,

    /// <summary>Round to the nearest ten rupees.</summary>
    Nearest10 = 3
}

/// <summary>Which contribution an EPF adjustment corrects.</summary>
public enum EpfAdjustmentTarget
{
    /// <summary>The employee's own EPF contribution — changes their net pay.</summary>
    EmployeeEpf = 1,

    /// <summary>The employer's EPF contribution — changes cost, not net pay.</summary>
    EmployerEpf = 2,

    /// <summary>The employer's ETF contribution.</summary>
    Etf = 3
}

public enum LoanStatus
{
    /// <summary>Being recovered.</summary>
    Active = 1,

    /// <summary>Fully repaid. Terminal.</summary>
    Settled = 2,

    /// <summary>Written off or cancelled before completion — kept for the trail.</summary>
    Cancelled = 3,

    /// <summary>Recovery paused, usually because the employee is suspended from payroll.</summary>
    OnHold = 4
}

public enum LoanTransactionType
{
    /// <summary>A scheduled instalment recovered through payroll.</summary>
    Installment = 1,

    /// <summary>Paid directly, outside payroll — an early settlement in part or full.</summary>
    Settlement = 2,

    /// <summary>A correction to the balance, in either direction.</summary>
    Adjustment = 3
}

/// <summary>
/// Who a bulk value change reaches on the Common Value Entry screen.
///
/// The three differ in a way worth being explicit about: the first creates a value for
/// people who had none, the second only moves those who already had one, and the third
/// touches a single month rather than the standing figure.
/// </summary>
public enum CommonValueScope
{
    /// <summary>Every active employee, whether or not they already had this item.</summary>
    AllActiveEmployees = 1,

    /// <summary>Only employees who already have a value for this item — a revision, not a grant.</summary>
    EmployeesWithItem = 2,

    /// <summary>The amounts entered against this item in the current month's transactions.</summary>
    CurrentMonthlyTransaction = 3
}

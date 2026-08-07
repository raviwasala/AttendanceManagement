namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// One allowance or deduction, for one employee, in one payroll month.
///
/// The counterpart to <see cref="EmployeeSalaryComponent"/>, and the distinction decides
/// what a payslip says. An EmployeeSalaryComponent is standing: a transport allowance that
/// is part of the package and repeats until somebody ends it. A MonthlyTransaction happens
/// once and is gone: a bonus, arrears, a fine, a travelling incentive that differs every
/// month because it is settled against actual claims.
///
/// Keeping them apart is what lets a payslip be re-generated years later and come out the
/// same. Recording a one-off as a standing component would pay it every month afterwards;
/// recording a standing item as a one-off would have somebody re-key it twelve times a year
/// and eventually forget.
///
/// YearMonth is stored as yyyymm rather than a date because that is the grain the figure
/// actually has — an amount belongs to August 2026, not to any day within it. A DateTime
/// would invite the question of which day, and different screens would answer differently.
/// </summary>
public class MonthlyTransaction : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    /// <summary>The payroll month, as yyyymm — 202608 is August 2026.</summary>
    public int YearMonth { get; set; }

    /// <summary>
    /// Always positive, for an earning and a deduction alike. Which way it moves is the
    /// component's business, not the amount's: a deduction stored as a negative number
    /// would be subtracted twice the moment somebody sums earnings and deductions
    /// separately, and that mistake is invisible until a payslip is wrong.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Hours behind the amount, where the item is paid by time — an OT allowance, a shift
    /// callout. Optional, and recorded rather than calculated from: the amount is what gets
    /// paid, and deriving it here would mean a second, competing OT rate alongside the one
    /// <see cref="AttendanceSystem.Application.Services.AttendanceCalculator"/> already owns.
    /// Kept so a payslip line can read "12.5 hrs" and so the figure can be checked back
    /// against the claim.
    /// </summary>
    public decimal? Hours { get; set; }

    /// <summary>
    /// Why this figure, for anyone reading the month back later — "Sept claim, 3 site
    /// visits". A one-off has no standing rule to justify it, so without a note the only
    /// record of the reason is whoever typed it.
    /// </summary>
    public string? Remarks { get; set; }
}

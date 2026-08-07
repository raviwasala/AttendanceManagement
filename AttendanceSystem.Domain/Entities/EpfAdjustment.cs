using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A correction to one employee's EPF or ETF for one month.
///
/// Adjusts the payslip <i>and</i> carries into the return, which is why it is a record rather
/// than an edit to a computed figure. Overwriting the contribution directly would leave the
/// payslip and the filed return unable to explain each other; a separate adjustment keeps the
/// original calculation intact and states what was added to it, and why.
/// </summary>
public class EpfAdjustment : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>The month being corrected — usually an earlier one than the month it is paid in.</summary>
    public int Year { get; set; }
    public int Month { get; set; }

    public EpfAdjustmentTarget Target { get; set; } = EpfAdjustmentTarget.EmployeeEpf;

    /// <summary>
    /// Signed. Negative recovers an over-contribution, positive collects arrears — both
    /// happen, and forcing a separate "direction" field would let the sign and the direction
    /// disagree.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>Why. Printed on the supplementary return, so it is required rather than a note.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Included in the statutory return as well as the payslip. Off for a correction that
    /// only moves money between the employee and the company — a refund of something
    /// deducted in error that was never remitted.
    /// </summary>
    public bool AffectsReturn { get; set; } = true;

    /// <summary>
    /// The payroll run that carried this adjustment. Null until it is picked up, which is
    /// what stops the same correction being applied twice.
    /// </summary>
    public int? AppliedInPayrollPeriodId { get; set; }
    public PayrollPeriod? AppliedInPayrollPeriod { get; set; }

    public DateTime? AppliedAt { get; set; }
}

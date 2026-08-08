using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// One raise, for one employee, on one date.
///
/// The employee's current basic lives on <see cref="EmployeePayrollInfo.BasicSalaryOverride"/>
/// — one number, which an increment overwrites. This table is the trail behind that number:
/// what it was, what it became, by how much, on whose authority. Without it the only record
/// of a raise is that the figure is different from last time somebody looked.
///
/// Both the previous and the new basic are stored rather than just the increment. Deriving
/// one from the other looks equivalent and is not: two increments applied in the same month,
/// or a salary corrected by hand in between, and the arithmetic stops reconstructing what
/// actually happened.
/// </summary>
public class SalaryIncrement : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>The date the raise takes effect, which is not the date it was keyed.</summary>
    public DateTime EffectiveDate { get; set; }

    public decimal PreviousBasic { get; set; }
    public decimal NewBasic { get; set; }

    /// <summary>The figure as it was entered — 2500, or 7.5 for a percentage.</summary>
    public decimal IncrementValue { get; set; }

    public IncrementBasis Basis { get; set; } = IncrementBasis.Amount;

    /// <summary>Annual review, promotion, correction — why this raise happened.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Set when the increment was applied to a group rather than one person, so a batch can
    /// be seen — and questioned — as one act instead of forty unrelated ones.
    /// </summary>
    public Guid? BatchId { get; set; }

    /// <summary>
    /// Proposed, confirmed or turned down.
    ///
    /// While Pending, <see cref="NewBasic"/> is what the salary WOULD become — the employee is
    /// still paid <see cref="PreviousBasic"/>. Nothing reads a pending row as pay. Keeping the
    /// proposal and the payment in one row rather than two tables is deliberate: the figure
    /// that was approved is then, unarguably, the figure that took effect.
    /// </summary>
    public IncrementStatus Status { get; set; } = IncrementStatus.Pending;

    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedBy { get; set; }

    /// <summary>Why a proposal was turned down. Kept so it is not simply re-proposed.</summary>
    public string? RejectionReason { get; set; }
}

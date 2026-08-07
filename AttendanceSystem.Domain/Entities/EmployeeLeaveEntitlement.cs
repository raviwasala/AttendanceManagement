namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// One employee's leave entitlement for one type, in one year.
///
/// Entitlement was a single figure on <see cref="LeaveType"/>, shared by everyone. That is
/// right as a default and wrong as the only answer: entitlement commonly varies with service
/// length, grade or contract, and a company-wide number forces a separate leave type per
/// variation.
///
/// Absent means "use the leave type's figure", so only the exceptions are recorded and a
/// change to the default still moves everybody who has not been singled out.
/// </summary>
public class EmployeeLeaveEntitlement : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    /// <summary>
    /// Per year because entitlement is granted annually and carried forward separately.
    /// A row for 2026 says nothing about 2027.
    /// </summary>
    public int Year { get; set; }

    public int EntitledDays { get; set; }

    /// <summary>
    /// Days carried in from the previous year, where the leave type allows it. Held apart
    /// from the entitlement so a report can show what was granted and what was inherited.
    /// </summary>
    public int CarriedForwardDays { get; set; }

    public string? Notes { get; set; }

    /// <summary>Entitlement plus anything carried in — what the employee may actually take.</summary>
    public int TotalDays => EntitledDays + CarriedForwardDays;
}

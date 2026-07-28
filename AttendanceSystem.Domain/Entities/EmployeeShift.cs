namespace AttendanceSystem.Domain.Entities;

/// <summary>Assignment of a shift to an employee.</summary>
public class EmployeeShift : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

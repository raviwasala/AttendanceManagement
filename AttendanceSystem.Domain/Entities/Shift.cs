namespace AttendanceSystem.Domain.Entities;

/// <summary>Work shift definition.</summary>
public class Shift : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GraceMinutes { get; set; }
    public string WeeklyOffDays { get; set; } = "Saturday,Sunday";
    public bool IsActive { get; set; } = true;

    public ICollection<EmployeeShift> EmployeeShifts { get; set; } = new List<EmployeeShift>();
}

namespace AttendanceSystem.Domain.Entities;

/// <summary>Monthly attendance summary per employee.</summary>
public class AttendanceSummary : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int Month { get; set; }
    public int Year { get; set; }
    public int TotalDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }
    public int LeaveDays { get; set; }
    public int HolidayDays { get; set; }
    public double TotalWorkingHours { get; set; }
}

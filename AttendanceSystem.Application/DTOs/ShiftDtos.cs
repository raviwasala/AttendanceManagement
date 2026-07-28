using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

public class ShiftDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GraceMinutes { get; set; }
    public string WeeklyOffDays { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string StartTimeDisplay => DateTime.Today.Add(StartTime).ToString("hh:mm tt");
    public string EndTimeDisplay => DateTime.Today.Add(EndTime).ToString("hh:mm tt");
}

public class SaveShiftDto
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required] public TimeSpan StartTime { get; set; }
    [Required] public TimeSpan EndTime { get; set; }
    [Range(0, 60)] public int GraceMinutes { get; set; }
    public string WeeklyOffDays { get; set; } = "Saturday,Sunday";
    public bool IsActive { get; set; } = true;
}

public class AssignShiftDto
{
    [Required] public int EmployeeId { get; set; }
    [Required] public int ShiftId { get; set; }
    [Required] public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class EmployeeShiftDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string StartTimeDisplay { get; set; } = string.Empty;
    public string EndTimeDisplay { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

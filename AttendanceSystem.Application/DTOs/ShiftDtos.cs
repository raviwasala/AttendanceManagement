using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

public class ShiftDto
{
    public int Id { get; set; }
    public string? ShiftCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GraceMinutes { get; set; }
    public int GraceOutMinutes { get; set; }
    public bool IsNightShift { get; set; }
    public int BreakMinutes { get; set; }
    public double StandardWorkingHours { get; set; }
    public int OtStartAfterMinutes { get; set; }
    public bool OtCountsFromShiftEnd { get; set; }
    public bool IsOtEnabled { get; set; }
    public string WeeklyOffDays { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public string StartTimeDisplay => DateTime.Today.Add(StartTime).ToString("hh:mm tt");
    public string EndTimeDisplay => DateTime.Today.Add(EndTime).ToString("hh:mm tt");

    /// <summary>Span in hours, handling a night shift that crosses midnight.</summary>
    public double SpanHours =>
        Math.Round((EndTime > StartTime
            ? EndTime - StartTime
            : EndTime.Add(TimeSpan.FromDays(1)) - StartTime).TotalHours, 2);

    public double EffectiveStandardHours =>
        StandardWorkingHours > 0 ? StandardWorkingHours : Math.Max(0, SpanHours - BreakMinutes / 60.0);
}

public class SaveShiftDto
{
    public int Id { get; set; }
    [MaxLength(20)] public string? ShiftCode { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required] public TimeSpan StartTime { get; set; }
    [Required] public TimeSpan EndTime { get; set; }

    [Range(0, 240)] public int GraceMinutes { get; set; }
    [Range(0, 240)] public int GraceOutMinutes { get; set; }

    /// <summary>Set when the shift crosses midnight. Validated against the times on save.</summary>
    public bool IsNightShift { get; set; }

    [Range(0, 480)] public int BreakMinutes { get; set; }
    [Range(0, 24)] public double StandardWorkingHours { get; set; }
    [Range(0, 480)] public int OtStartAfterMinutes { get; set; }
    public bool OtCountsFromShiftEnd { get; set; } = true;
    public bool IsOtEnabled { get; set; } = true;

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

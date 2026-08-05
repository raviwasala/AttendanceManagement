using AttendanceSystem.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

public class AttendanceLogDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public bool IsLate { get; set; }
    public bool IsEarlyLeave { get; set; }
    public int? LateMinutes { get; set; }
    public int? EarlyLeaveMinutes { get; set; }
    public double? WorkingHours { get; set; }
    public string? Remarks { get; set; }
    public bool IsManual { get; set; }
    public string CheckInDisplay => CheckIn?.ToString("hh:mm tt") ?? "-";
    public string CheckOutDisplay => CheckOut?.ToString("hh:mm tt") ?? "-";
}

public class CheckInDto
{
    [Required] public int EmployeeId { get; set; }
    public DateTime CheckInTime { get; set; } = DateTime.Now;
    public string? Remarks { get; set; }
}

public class CheckOutDto
{
    [Required] public int AttendanceLogId { get; set; }
    public DateTime CheckOutTime { get; set; } = DateTime.Now;
    public string? Remarks { get; set; }
}

public class EditAttendanceDto
{
    public int Id { get; set; }
    [Required] public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
}

public class AttendanceSummaryDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public int TotalDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }
    public int LeaveDays { get; set; }
    public int HolidayDays { get; set; }
    public double TotalWorkingHours { get; set; }

    /// <summary>Late days the shift tolerates in a month. 0 means no limit was configured.</summary>
    public int LateAllowance { get; set; }

    /// <summary>True when LateDays has gone past the allowance. Reporting only.</summary>
    public bool IsOverLateAllowance => LateAllowance > 0 && LateDays > LateAllowance;

    public string LateAllowanceDisplay =>
        LateAllowance > 0 ? $"{LateDays} of {LateAllowance}" : LateDays.ToString();

    public double AttendancePercentage =>
        TotalDays > 0 ? Math.Round((double)PresentDays / TotalDays * 100, 2) : 0;
}

public class DashboardStatsDto
{
    public int TotalEmployees { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int LateToday { get; set; }
    public int OnLeaveToday { get; set; }
    public double AttendancePercentage { get; set; }
    public List<AttendanceLogDto> RecentAttendance { get; set; } = new();
}

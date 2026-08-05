using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs;

/// <summary>Who the signed-in employee is, for the header of their own screens.</summary>
public class MyProfileDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public DateTime JoiningDate { get; set; }

    public string? ShiftName { get; set; }
    public string? ShiftTimes { get; set; }
}

public class MyAttendanceDayDto
{
    public DateTime Date { get; set; }
    public string DateDisplay { get; set; } = string.Empty;

    public string? ShiftName { get; set; }
    public string? ExpectedIn { get; set; }
    public string? ExpectedOut { get; set; }

    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }

    public int? LateMinutes { get; set; }
    public int? EarlyLeaveMinutes { get; set; }
    public double? WorkingHours { get; set; }
    public int? OvertimeMinutes { get; set; }

    public AttendanceStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();

    public bool IsHoliday { get; set; }
    public string? HolidayName { get; set; }
    public bool IsWeeklyOff { get; set; }
}

public class MyAttendanceDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;

    public int PresentDays { get; set; }
    public int LateDays { get; set; }
    public int AbsentDays { get; set; }
    public int LeaveDays { get; set; }
    public double TotalWorkingHours { get; set; }
    public int TotalOvertimeMinutes { get; set; }
    public int TotalLateMinutes { get; set; }

    public List<MyAttendanceDayDto> Days { get; set; } = new();
}

public class MyLeaveBalanceDto
{
    public int LeaveTypeId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public int Entitled { get; set; }
    public int Used { get; set; }
    public int Remaining { get; set; }
    public bool IsPaid { get; set; }
}

/// <summary>
/// An employee applying for their own leave.
///
/// Deliberately has no EmployeeId. The admin <c>ApplyLeaveDto</c> carries one because an
/// administrator legitimately applies on someone else's behalf; here the employee is taken
/// from the session, so the field cannot be supplied and therefore cannot be forged.
/// </summary>
public class ApplyMyLeaveDto
{
    [Required] public int LeaveTypeId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public class MyLeaveRequestDto
{
    public int Id { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public string? RejectionReason { get; set; }
    public DateTime AppliedOn { get; set; }
}

public class MyLeaveDto
{
    public int Year { get; set; }
    public int PendingCount { get; set; }
    public List<MyLeaveBalanceDto> Balances { get; set; } = new();
    public List<MyLeaveRequestDto> Requests { get; set; } = new();
}

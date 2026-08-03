namespace AttendanceSystem.Application.DTOs;

// ── Attendance trend ───────────────────────────────────────────────────────────

/// <summary>One day in the attendance trend.</summary>
public class AttendanceTrendPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;   // "Mon 03"
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int OnLeave { get; set; }

    /// <summary>Employees whose shift makes this a weekly off day, plus holidays.</summary>
    public int NonWorking { get; set; }

    public bool IsHoliday { get; set; }

    /// <summary>Checked in (present + late) as a percentage of employees expected to work.</summary>
    public double AttendancePercentage { get; set; }
}

public class AttendanceTrendDto
{
    public int Days { get; set; }
    public List<AttendanceTrendPointDto> Points { get; set; } = new();

    /// <summary>Mean attendance percentage across working days in the window.</summary>
    public double AverageAttendancePercentage { get; set; }

    /// <summary>Days in the window that had at least one attendance record — the trend is
    /// unreliable below a handful of these, so the UI can warn instead of implying a pattern.</summary>
    public int DaysWithData { get; set; }
}

// ── Punctuality ────────────────────────────────────────────────────────────────

public class LateEmployeeDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int LateCount { get; set; }
    public int TotalLateMinutes { get; set; }
    public double AverageLateMinutes { get; set; }
}

public class WeekdayPunctualityDto
{
    public string Day { get; set; } = string.Empty;
    public int CheckIns { get; set; }
    public int LateCount { get; set; }
    public double LatePercentage { get; set; }
}

public class DepartmentPunctualityDto
{
    public string Department { get; set; } = string.Empty;
    public int CheckIns { get; set; }
    public int LateCount { get; set; }
    public double LatePercentage { get; set; }
    public double AverageLateMinutes { get; set; }
}

public class PunctualityDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public int TotalCheckIns { get; set; }
    public int TotalLate { get; set; }
    public double LatePercentage { get; set; }
    public double AverageLateMinutes { get; set; }
    public int TotalEarlyLeave { get; set; }

    public List<LateEmployeeDto> TopLate { get; set; } = new();
    public List<WeekdayPunctualityDto> ByWeekday { get; set; } = new();
    public List<DepartmentPunctualityDto> ByDepartment { get; set; } = new();
}

// ── Leave overview ─────────────────────────────────────────────────────────────

public class LeaveTypeUtilisationDto
{
    public string LeaveType { get; set; } = string.Empty;
    public int AllowancePerEmployee { get; set; }
    public int TotalEntitlement { get; set; }   // allowance × active employees
    public int DaysTaken { get; set; }
    public double UtilisationPercentage { get; set; }
}

public class UpcomingLeaveDto
{
    public int LeaveRequestId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string LeaveType { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalDays { get; set; }
    public int StartsInDays { get; set; }
}

public class LeaveOverviewDto
{
    public int PendingCount { get; set; }
    public int OldestPendingDays { get; set; }
    public int OnLeaveToday { get; set; }
    public int ApprovedThisMonth { get; set; }

    public List<LeaveRequestDto> PendingRequests { get; set; } = new();
    public List<LeaveTypeUtilisationDto> Utilisation { get; set; } = new();
    public List<UpcomingLeaveDto> Upcoming { get; set; } = new();
}

// ── Operations health ──────────────────────────────────────────────────────────

public class OperationsIssueEmployeeDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

/// <summary>
/// Operational data-quality signals — the things that silently produce wrong attendance.
/// Each figure is a count of records needing human attention, not a business metric.
/// </summary>
public class OperationsHealthDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>Active employees with no BiometricEnrollId — device punches can never match them.</summary>
    public int MissingBiometricId { get; set; }
    public List<OperationsIssueEmployeeDto> MissingBiometricEmployees { get; set; } = new();

    /// <summary>Records with a check-in but no check-out, excluding today (still in progress).</summary>
    public int MissingCheckOut { get; set; }
    public List<OperationsIssueEmployeeDto> MissingCheckOutRecords { get; set; } = new();

    /// <summary>Employees with no shift covering today — they are never flagged late or early.</summary>
    public int WithoutShift { get; set; }
    public List<OperationsIssueEmployeeDto> WithoutShiftEmployees { get; set; } = new();

    public int ManualRecords { get; set; }
    public int DeviceRecords { get; set; }
    public double ManualPercentage { get; set; }

    /// <summary>Creation time of the most recent device-sourced record, if any.</summary>
    public DateTime? LastDeviceRecordAt { get; set; }
}

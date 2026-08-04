namespace AttendanceSystem.Application.DTOs;

/// <summary>One day in one employee's row of the monthly roster.</summary>
public class RosterDayDto
{
    public DateTime Date { get; set; }
    public int Day { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;

    /// <summary>Shift in force on this date, or null when the employee has no assignment covering it.</summary>
    public int? ShiftId { get; set; }
    public string? ShiftName { get; set; }
    public string? ShiftTimes { get; set; }

    /// <summary>
    /// True when this day is covered by a single-day assignment rather than inheriting a
    /// longer-running one. The distinction is the whole point of the screen: it tells the
    /// user which days they have deliberately changed.
    /// </summary>
    public bool IsOverride { get; set; }

    /// <summary>The assignment row behind this day — needed to clear a single-day override.</summary>
    public int? AssignmentId { get; set; }

    /// <summary>The shift's own weekly-off day, so the grid can grey it out.</summary>
    public bool IsWeeklyOff { get; set; }

    public bool IsHoliday { get; set; }
    public string? HolidayName { get; set; }
}

/// <summary>One employee's row across the month.</summary>
public class RosterEmployeeDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    /// <summary>The assignment the employee falls back to when a day has no override.</summary>
    public string? DefaultShiftName { get; set; }

    /// <summary>True when nothing covers this employee at all — they are never marked late.</summary>
    public bool HasNoAssignment { get; set; }

    public List<RosterDayDto> Days { get; set; } = new();
}

/// <summary>The whole month grid.</summary>
public class ShiftRosterDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int DaysInMonth { get; set; }

    public List<ShiftDto> AvailableShifts { get; set; } = new();
    public List<RosterEmployeeDto> Employees { get; set; } = new();

    /// <summary>Employees with no shift assignment at all — surfaced so it can be fixed.</summary>
    public int EmployeesWithoutAssignment { get; set; }
}

/// <summary>Sets or clears the shift for one employee on one date.</summary>
public class SetRosterDayDto
{
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; }

    /// <summary>Null clears the override so the day falls back to the employee's normal shift.</summary>
    public int? ShiftId { get; set; }
}

/// <summary>Applies one shift across a date range — for a week of nights, or a whole month.</summary>
public class SetRosterRangeDto
{
    public int EmployeeId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? ShiftId { get; set; }

    /// <summary>When true, weekly-off days in the range are left untouched.</summary>
    public bool SkipWeeklyOff { get; set; }
}

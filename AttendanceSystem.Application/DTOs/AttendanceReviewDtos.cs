using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs;

/// <summary>
/// One employee on one day: the shift they were rostered for, next to what the device
/// actually recorded. The whole point of the screen is the comparison, so both live on
/// the same row.
/// </summary>
public class AttendanceReviewRowDto
{
    /// <summary>
    /// The date this row is for. Carried on the row, not just the parent, because a range
    /// query returns many dates per employee.
    /// </summary>
    public DateTime Date { get; set; }
    public string DateDisplay { get; set; } = string.Empty;   // "Mon 03 Aug"

    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    // ── Rostered ──────────────────────────────────────────────────────────────
    public int? ShiftId { get; set; }
    public string? ShiftName { get; set; }
    public string? ExpectedIn { get; set; }     // "09:00"
    public string? ExpectedOut { get; set; }    // "18:00"
    public int GraceMinutes { get; set; }

    /// <summary>True when no shift covers this date — nothing can be judged late.</summary>
    public bool HasNoShift { get; set; }

    // ── Actual ────────────────────────────────────────────────────────────────

    /// <summary>Attendance record id, or 0 when nothing was recorded for this day.</summary>
    public int AttendanceId { get; set; }

    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }

    /// <summary>Times as HH:mm for the editable inputs — avoids timezone games in the browser.</summary>
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }

    public bool IsLate { get; set; }
    public int? LateMinutes { get; set; }

    /// <summary>
    /// Which late arrival this is within its calendar month — 1 for the first, 2 for the
    /// second, and so on. 0 when the day is not late.
    /// </summary>
    public int LateOccurrence { get; set; }

    /// <summary>Late days the shift tolerates per month. 0 means no limit.</summary>
    public int LateAllowance { get; set; }

    /// <summary>
    /// True when this late arrival is past the shift's monthly allowance. Reporting only —
    /// status, hours and overtime are unaffected.
    /// </summary>
    public bool IsOverLateAllowance => LateAllowance > 0 && LateOccurrence > LateAllowance;

    public bool IsEarlyLeave { get; set; }
    public int? EarlyLeaveMinutes { get; set; }

    /// <summary>Paid hours, after the shift's break is deducted.</summary>
    public double? WorkingHours { get; set; }

    /// <summary>Check-out minus check-in, before the break deduction.</summary>
    public double? GrossHours { get; set; }

    public int? OvertimeMinutes { get; set; }

    /// <summary>True when the rostered shift runs past midnight — the out time is the next day.</summary>
    public bool IsNightShift { get; set; }
    public int BreakMinutes { get; set; }

    public AttendanceStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();

    /// <summary>True when a person entered or corrected this rather than the device.</summary>
    public bool IsManual { get; set; }
    public string? Remarks { get; set; }

    public bool IsHoliday { get; set; }
    public string? HolidayName { get; set; }
    public bool IsWeeklyOff { get; set; }
}

public class AttendanceReviewDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string RangeDisplay { get; set; } = string.Empty;

    /// <summary>True when the range covers more than one day — the UI shows a Date column.</summary>
    public bool IsRange { get; set; }

    public int DayCount { get; set; }
    public int TotalEmployees { get; set; }

    /// <summary>Counts across every row in the range, not per day.</summary>
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int OnLeave { get; set; }

    /// <summary>Checked in but never out — the rows that most need a correction.</summary>
    public int MissingCheckOut { get; set; }

    public int TotalLateMinutes { get; set; }
    public double TotalWorkingHours { get; set; }
    public int TotalOvertimeMinutes { get; set; }

    /// <summary>Set when the result was capped, so the UI can say so rather than mislead.</summary>
    public bool Truncated { get; set; }

    public List<AttendanceReviewRowDto> Rows { get; set; } = new();
}

/// <summary>
/// Saves corrected in/out times for one employee on one date.
///
/// Keyed on employee + date rather than an attendance id, because the row being corrected
/// may not exist yet — an absent employee has no record until someone records one.
/// </summary>
public class SaveAttendanceEntryDto
{
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; }

    /// <summary>"HH:mm", or null/empty to clear.</summary>
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }

    public string? Remarks { get; set; }

    /// <summary>
    /// Optional manual status. Null means derive it from the times and the shift, which is
    /// the normal case — an explicit value is for marking someone On Leave or Holiday.
    /// </summary>
    public AttendanceStatus? Status { get; set; }
}

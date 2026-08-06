using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

/// <summary>
/// One thing standing between a month and being closed.
///
/// Separated into blocking and advisory because they are answered differently: a pending
/// overtime claim must be decided before the month can be paid, while an employee with no
/// biometric id is worth knowing about but does not make the figures wrong.
/// </summary>
public class MonthEndCheckDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>What was found, in plain words — "3 claims awaiting approval".</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>How many records are affected. 0 means the check passed.</summary>
    public int Count { get; set; }

    /// <summary>True when this must be cleared before the month can close.</summary>
    public bool IsBlocking { get; set; }

    /// <summary>Where to go and fix it, when there is a screen for it.</summary>
    public string? ActionUrl { get; set; }
    public string? ActionLabel { get; set; }

    public bool Passed => Count == 0;
}

/// <summary>Whether a month can be closed, and what is in the way.</summary>
public class MonthEndStatusDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PeriodDisplay => $"{FromDate:MMMM yyyy}";

    /// <summary>True once the period is locked — the month is closed.</summary>
    public bool IsClosed { get; set; }
    public string? ClosedReason { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    public List<MonthEndCheckDto> Checks { get; set; } = [];

    public int EmployeeCount { get; set; }

    /// <summary>Blocking checks that have not passed. Empty means the month can be closed.</summary>
    public List<MonthEndCheckDto> Blockers =>
        Checks.Where(c => c.IsBlocking && !c.Passed).ToList();

    public List<MonthEndCheckDto> Warnings =>
        Checks.Where(c => !c.IsBlocking && !c.Passed).ToList();

    public bool CanClose => !IsClosed && Blockers.Count == 0;
}

/// <summary>
/// One employee's month, with everything payroll needs in a single row.
///
/// Attendance and overtime used to be exported separately and joined by hand in a
/// spreadsheet — the step most likely to go wrong, and silent when it did.
/// </summary>
public class PayrollRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;

    public int TotalDays { get; set; }
    public int WorkingDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LeaveDays { get; set; }
    public int HolidayDays { get; set; }

    public int LateDays { get; set; }
    public int LateMinutes { get; set; }

    public double WorkingHours { get; set; }

    /// <summary>Approved overtime only. Pending claims are deliberately excluded.</summary>
    public double ApprovedOtHours { get; set; }

    /// <summary>Approved overtime worked on a weekly off or holiday — usually paid at a higher rate.</summary>
    public double PremiumOtHours { get; set; }

    public double AttendancePercentage { get; set; }
}

public class PayrollExportDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string PeriodDisplay { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<PayrollRowDto> Rows { get; set; } = [];

    public int EmployeeCount => Rows.Count;
    public double TotalWorkingHours => Math.Round(Rows.Sum(r => r.WorkingHours), 2);
    public double TotalOtHours => Math.Round(Rows.Sum(r => r.ApprovedOtHours), 2);
}

public class CloseMonthDto
{
    [Range(1, 12)] public int Month { get; set; }
    [Range(2000, 2100)] public int Year { get; set; }

    /// <summary>Recorded on the lock, so a reopened month still shows why it was closed.</summary>
    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Closes despite non-blocking warnings. Blockers are never overridable — a month with
    /// undecided overtime cannot be paid correctly, whoever asks.
    /// </summary>
    public bool AcknowledgeWarnings { get; set; }
}

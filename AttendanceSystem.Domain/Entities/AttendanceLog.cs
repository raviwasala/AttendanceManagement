using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>Single day attendance record for an employee.</summary>
public class AttendanceLog : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime AttendanceDate { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; }
    public bool IsLate { get; set; }
    public bool IsEarlyLeave { get; set; }
    public int? LateMinutes { get; set; }
    public int? EarlyLeaveMinutes { get; set; }
    /// <summary>Paid time actually worked, after the shift's unpaid break is deducted.</summary>
    public double? WorkingHours { get; set; }

    /// <summary>Raw check-out minus check-in, before the break deduction. Kept so a
    /// disagreement about hours can be traced without recomputing from the shift.</summary>
    public double? GrossHours { get; set; }

    /// <summary>Overtime earned, in minutes. Null when the shift has overtime disabled.</summary>
    public int? OvertimeMinutes { get; set; }

    public string? Remarks { get; set; }
    public bool IsManual { get; set; }
}

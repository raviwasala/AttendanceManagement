using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Attendance Record entity - captures check-in/check-out and status
/// </summary>
[Table("AttendanceRecords")]
public class AttendanceRecord
{
    [Key]
    public int AttendanceId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime AttendanceDate { get; set; }

    public TimeSpan? CheckInTime { get; set; }

    public TimeSpan? CheckOutTime { get; set; }

    /// <summary>
    /// Status: Present, Absent, Late, OnLeave, HalfDay, etc.
    /// </summary>
    [StringLength(50)]
    public string Status { get; set; } = "Absent";

    /// <summary>
    /// Minutes late (calculated from shift start time and grace period)
    /// </summary>
    public int? LateMinutes { get; set; }

    /// <summary>
    /// Total working hours on this day
    /// </summary>
    public decimal? WorkedHours { get; set; }

    /// <summary>
    /// Notes / remarks for the attendance record
    /// </summary>
    [StringLength(500)]
    public string? Remarks { get; set; }

    /// <summary>
    /// Biometric verification ID (which device/scanner was used)
    /// </summary>
    [StringLength(100)]
    public string? BiometricDeviceId { get; set; }

    /// <summary>
    /// Whether attendance was manually edited
    /// </summary>
    public bool IsManualEntry { get; set; } = false;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? ModifiedDate { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Navigation property
    /// </summary>
    [ForeignKey("EmployeeId")]
    public virtual Employee? Employee { get; set; }
}

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Maps one device's user id to a system employee.
///
/// Scoped per device on purpose. Employee.BiometricEnrollId is a single global value, which
/// breaks in a multi-device deployment: the same person can hold different ids on two devices,
/// and two people at different branches can hold the same id — silently attributing one
/// employee's attendance to another. BiometricEnrollId remains the auto-match hint when
/// building these rows; this table is the authority.
/// </summary>
public class DeviceUserMapping : BaseEntity
{
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    /// <summary>The device's user id. String — see <see cref="DevicePunch.DeviceUserId"/>.</summary>
    public string DeviceUserId { get; set; } = string.Empty;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>Name as enrolled on the device — helps an operator confirm a match.</summary>
    public string? DeviceUserName { get; set; }
}

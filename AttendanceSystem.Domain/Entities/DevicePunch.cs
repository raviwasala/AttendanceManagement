namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A raw punch exactly as the device reported it. Append-only — never edited, never deleted.
///
/// This table is what makes the integration recoverable. It is the idempotency boundary
/// (a unique index on DeviceId + DeviceUserId + PunchTime rejects re-downloads) and the replay
/// source: if the punch-pairing logic is wrong, or an employee is mapped after the fact, the
/// daily attendance can be rebuilt from here without contacting the device again.
///
/// Deliberately does NOT inherit BaseEntity: there is nothing to soft-delete or modify, and a
/// query filter would risk hiding raw evidence. Same reasoning as <see cref="AuditLog"/>.
/// </summary>
public class DevicePunch
{
    public long Id { get; set; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    /// <summary>
    /// The device's own user id, stored as reported. String rather than int: user ids are not
    /// reliably numeric across ZKTeco models, and parsing would fail on alphanumeric ids.
    /// </summary>
    public string DeviceUserId { get; set; } = string.Empty;

    public DateTime PunchTime { get; set; }

    /// <summary>Verification method reported by the device (finger, card, password).</summary>
    public int VerifyMode { get; set; }

    /// <summary>The device's own in/out flag. Advisory only — pairing is by first/last punch.</summary>
    public int InOutMode { get; set; }

    public DateTime DownloadedAt { get; set; }

    /// <summary>
    /// Null while the device user id has no mapping. The punch is still stored: discarding
    /// unmatched punches at download time loses them permanently, whereas keeping them lets a
    /// later mapping recover the history.
    /// </summary>
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    /// <summary>True once this punch has contributed to an AttendanceLog row.</summary>
    public bool IsProcessed { get; set; }
}

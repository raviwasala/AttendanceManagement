using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// One row per sync attempt, successful or not. Backs both the "Sync History" and
/// "Error Logs" screens — they are the same data with a different filter, so they are
/// the same table.
///
/// Append-only, like <see cref="DevicePunch"/> and <see cref="AuditLog"/>.
/// </summary>
public class DeviceSyncLog
{
    public long Id { get; set; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public SyncTrigger Trigger { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SyncOutcome Outcome { get; set; }

    public int PunchesRead { get; set; }

    /// <summary>Read minus inserted is the number of duplicates the unique index rejected.</summary>
    public int PunchesInserted { get; set; }

    /// <summary>Punches whose device user id has no mapping — the number to act on.</summary>
    public int PunchesUnmapped { get; set; }

    public int AttendanceRecordsAffected { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Null for scheduled runs; set when a person pressed Sync Now.</summary>
    public int? TriggeredByUserId { get; set; }
}

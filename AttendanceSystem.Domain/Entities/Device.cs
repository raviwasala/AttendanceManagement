using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A physical fingerprint terminal reachable over TCP/IP.
///
/// Sync state lives on the device rather than in a separate table because there is exactly
/// one current position per device, and keeping it here makes "when did this last work?"
/// answerable without a join. Historical detail belongs in <see cref="DeviceSyncLog"/>.
/// </summary>
public class Device : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 4370;

    /// <summary>Device communication key ("comm key"/password). Null or 0 when unset.</summary>
    public int? CommKey { get; set; }

    /// <summary>Read from the device; kept for support and for spotting a swapped unit.</summary>
    public string? SerialNumber { get; set; }
    public string? Model { get; set; }

    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    /// <summary>Inactive devices are skipped by automatic sync and hidden from status polling.</summary>
    public bool IsActive { get; set; } = true;
    public bool AutoSyncEnabled { get; set; } = true;

    // ── Sync state ────────────────────────────────────────────────────────────

    /// <summary>
    /// Watermark for incremental download. The next sync requests punches from
    /// (this value − overlap), never from this value exactly — see the design note on
    /// clock drift in docs/DEVICE-INTEGRATION-DESIGN.md §5.
    /// </summary>
    public DateTime? LastPunchTimeSynced { get; set; }

    /// <summary>Set when a sync begins; used to stop a second sync starting on the same device.</summary>
    public DateTime? LastSyncStartedAt { get; set; }
    public DateTime? LastSuccessfulSyncAt { get; set; }

    /// <summary>Reset to 0 on any successful contact. Drives the Error status threshold.</summary>
    public int ConsecutiveFailures { get; set; }

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>Last time the device answered — "Online" means reachable when last asked.</summary>
    public DateTime? LastSeenAt { get; set; }

    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;
    public string? LastError { get; set; }

    public ICollection<DeviceUserMapping> UserMappings { get; set; } = new List<DeviceUserMapping>();
}

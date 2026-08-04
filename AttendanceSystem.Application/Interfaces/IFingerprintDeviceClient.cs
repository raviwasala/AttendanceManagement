using AttendanceSystem.Common.Models;

namespace AttendanceSystem.Application.Interfaces;

/// <summary>Where to reach a device and how long to wait for it.</summary>
public record DeviceConnection(string IpAddress, int Port, int? CommKey, TimeSpan Timeout)
{
    public static TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);
}

/// <summary>What a probe learned about a device.</summary>
public record DeviceProbeResult(
    bool IsReachable,
    int ResponseMs,
    DateTime? DeviceTime = null,
    string? SerialNumber = null,
    string? Model = null,
    string? Message = null);

/// <summary>
/// The single boundary between this system and fingerprint hardware.
///
/// Everything device-specific lives behind here. No ZKTeco type, SDK handle or protocol
/// constant may appear in a service, controller or view — the moment one does, swapping or
/// adding a device vendor stops being a contained change.
///
/// Phase 1 needs only a probe. Reading attendance logs and enrolled users are added in
/// phase 2, when the protocol client lands; see docs/DEVICE-INTEGRATION-DESIGN.md §2.
/// </summary>
public interface IFingerprintDeviceClient
{
    /// <summary>
    /// Contacts the device and reports what came back. Never throws for an unreachable
    /// device — that is an expected outcome, returned as <c>IsReachable = false</c>.
    /// </summary>
    Task<Result<DeviceProbeResult>> ProbeAsync(DeviceConnection connection, CancellationToken ct = default);
}

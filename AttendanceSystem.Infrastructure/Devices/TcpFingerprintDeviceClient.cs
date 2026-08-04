using System.Diagnostics;
using System.Net.Sockets;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;

namespace AttendanceSystem.Infrastructure.Devices;

/// <summary>
/// Phase 1 device client: a TCP reachability probe.
///
/// This opens a socket to the device's service port and reports whether it answered. That is
/// genuinely most of the diagnostic value — it distinguishes "powered off / wrong IP / blocked
/// by firewall / not listening" from "device is there", which is where nearly every
/// installation problem lives.
///
/// It does NOT speak the ZKTeco protocol, so it cannot yet report device time, serial number
/// or model; those fields come back null. The protocol client arrives in phase 2 and replaces
/// this class behind the same interface — see docs/DEVICE-INTEGRATION-DESIGN.md §2.
/// </summary>
public class TcpFingerprintDeviceClient : IFingerprintDeviceClient
{
    public async Task<Result<DeviceProbeResult>> ProbeAsync(
        DeviceConnection connection, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connection.IpAddress))
            return Result<DeviceProbeResult>.Failure("Device has no IP address configured.");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();

            // TcpClient has no timeout of its own — an unreachable host would otherwise hang
            // for the OS connect timeout (~20s+), stalling the request and any sync loop.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(connection.Timeout);

            await client.ConnectAsync(connection.IpAddress, connection.Port, timeoutCts.Token);
            stopwatch.Stop();

            return Result<DeviceProbeResult>.Success(new DeviceProbeResult(
                IsReachable: true,
                ResponseMs: (int)stopwatch.ElapsedMilliseconds,
                Message: "Device responded on the configured port."));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Our timeout fired rather than the caller cancelling.
            stopwatch.Stop();
            return Result<DeviceProbeResult>.Success(new DeviceProbeResult(
                IsReachable: false,
                ResponseMs: (int)stopwatch.ElapsedMilliseconds,
                Message: $"No response within {connection.Timeout.TotalSeconds:0}s. "
                       + "Check the device is powered on and the IP address is correct."));
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            // An unreachable device is an expected state, not an error to log as a fault.
            return Result<DeviceProbeResult>.Success(new DeviceProbeResult(
                IsReachable: false,
                ResponseMs: (int)stopwatch.ElapsedMilliseconds,
                Message: Describe(ex)));
        }
        catch (Exception ex)
        {
            AppLogger.Error("TcpFingerprintDeviceClient.ProbeAsync", ex);
            return Result<DeviceProbeResult>.Failure("Unexpected error while contacting the device.");
        }
    }

    /// <summary>Turns socket error codes into something an installer can act on.</summary>
    private static string Describe(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused =>
            "Connection refused — something answered at that address but not on this port. Check the port number.",
        SocketError.HostUnreachable or SocketError.NetworkUnreachable =>
            "Host unreachable — the device is not on this network. Check the IP address and subnet.",
        SocketError.TimedOut =>
            "Timed out — the device did not respond. Check it is powered on and not blocked by a firewall.",
        _ => $"Could not connect ({ex.SocketErrorCode})."
    };
}

using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Fingerprint device registry: CRUD, branch assignment, connectivity and status.
///
/// Talks to hardware only through <see cref="IFingerprintDeviceClient"/>, and only to probe.
/// Attendance synchronisation is a separate service so that managing devices stays possible
/// even when the sync path is broken.
/// </summary>
public class DeviceService : IDeviceService
{
    /// <summary>Device clock difference worth warning about — below this, drift is harmless.</summary>
    private static readonly TimeSpan ClockDriftTolerance = TimeSpan.FromMinutes(2);

    /// <summary>Failures before a device is called Error rather than merely Offline.</summary>
    private const int ErrorThreshold = 3;

    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    private readonly IFingerprintDeviceClient _client;

    public DeviceService(IUnitOfWork uow, IAuditService audit,
        ICurrentUserContext currentUser, IFingerprintDeviceClient client)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
        _client = client;
    }

    public async Task<Result<IEnumerable<DeviceDto>>> GetAllAsync()
    {
        try
        {
            var devices = (await _uow.Devices.GetAllAsync()).ToList();
            return Result<IEnumerable<DeviceDto>>.Success(await MapManyAsync(devices));
        }
        catch (Exception ex)
        {
            AppLogger.Error("DeviceService.GetAllAsync", ex);
            return Result<IEnumerable<DeviceDto>>.Failure("Failed to load devices.");
        }
    }

    public async Task<Result<IEnumerable<DeviceDto>>> GetByBranchAsync(int branchId)
    {
        try
        {
            var devices = (await _uow.Devices.FindAsync(d => d.BranchId == branchId && !d.IsDeleted)).ToList();
            return Result<IEnumerable<DeviceDto>>.Success(await MapManyAsync(devices));
        }
        catch (Exception ex)
        {
            AppLogger.Error("DeviceService.GetByBranchAsync", ex);
            return Result<IEnumerable<DeviceDto>>.Failure("Failed to load devices for the branch.");
        }
    }

    public async Task<Result<DeviceDto>> GetByIdAsync(int id)
    {
        try
        {
            var device = await _uow.Devices.GetByIdAsync(id);
            if (device == null) return Result<DeviceDto>.Failure("Device not found.");

            var mapped = await MapManyAsync(new List<Device> { device });
            return Result<DeviceDto>.Success(mapped.First());
        }
        catch (Exception ex)
        {
            AppLogger.Error("DeviceService.GetByIdAsync", ex);
            return Result<DeviceDto>.Failure("Failed to load the device.");
        }
    }

    public async Task<Result<DeviceDto>> SaveAsync(SaveDeviceDto dto)
    {
        try
        {
            var validation = await ValidateAsync(dto);
            if (!validation.IsSuccess) return Result<DeviceDto>.Failure(validation.ErrorMessage!);

            Device device;

            if (dto.Id == 0)
            {
                device = new Device
                {
                    Name = dto.Name.Trim(),
                    IpAddress = dto.IpAddress.Trim(),
                    Port = dto.Port,
                    CommKey = dto.CommKey,
                    BranchId = dto.BranchId,
                    IsActive = dto.IsActive,
                    AutoSyncEnabled = dto.AutoSyncEnabled,
                    Status = DeviceStatus.Unknown,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                };
                await _uow.Devices.AddAsync(device);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync(AppConstants.Modules.Devices, "Create",
                    _currentUser.UserId, nameof(Device), device.Id);
            }
            else
            {
                var existing = await _uow.Devices.GetByIdAsync(dto.Id);
                if (existing == null) return Result<DeviceDto>.Failure("Device not found.");

                // Moving a device to a new address invalidates what we knew about the old one.
                var endpointChanged = existing.IpAddress != dto.IpAddress.Trim() || existing.Port != dto.Port;

                existing.Name = dto.Name.Trim();
                existing.IpAddress = dto.IpAddress.Trim();
                existing.Port = dto.Port;
                existing.BranchId = dto.BranchId;
                existing.IsActive = dto.IsActive;
                existing.AutoSyncEnabled = dto.AutoSyncEnabled;

                // Null means "leave the stored key alone" — the UI never receives it back, so
                // an unchanged edit form must not blank it. 0 clears it deliberately.
                if (dto.CommKey.HasValue)
                    existing.CommKey = dto.CommKey.Value == 0 ? null : dto.CommKey.Value;

                if (endpointChanged)
                {
                    existing.Status = DeviceStatus.Unknown;
                    existing.ConsecutiveFailures = 0;
                    existing.LastError = null;
                    existing.LastSeenAt = null;
                }

                existing.ModifiedBy = _currentUser.UserId;
                existing.ModifiedAt = DateTime.Now;

                await _uow.Devices.UpdateAsync(existing);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync(AppConstants.Modules.Devices, "Update",
                    _currentUser.UserId, nameof(Device), existing.Id);

                device = existing;
            }

            var result = await MapManyAsync(new List<Device> { device });
            return Result<DeviceDto>.Success(result.First());
        }
        catch (Exception ex)
        {
            AppLogger.Error("DeviceService.SaveAsync", ex);
            return Result<DeviceDto>.Failure("Failed to save the device.");
        }
    }

    public async Task<Result> DeleteAsync(int id)
    {
        try
        {
            var device = await _uow.Devices.GetByIdAsync(id);
            if (device == null) return Result.Failure("Device not found.");

            // Soft delete only. Downloaded punches reference this device and are the evidence
            // behind existing attendance records — removing the row would orphan that history.
            device.IsDeleted = true;
            device.IsActive = false;
            device.AutoSyncEnabled = false;
            device.ModifiedBy = _currentUser.UserId;
            device.ModifiedAt = DateTime.Now;

            await _uow.Devices.UpdateAsync(device);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Devices, "Delete",
                _currentUser.UserId, nameof(Device), id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("DeviceService.DeleteAsync", ex);
            return Result.Failure("Failed to delete the device.");
        }
    }

    public async Task<Result<DeviceTestResultDto>> TestConnectionAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var device = await _uow.Devices.GetByIdAsync(id);
            if (device == null) return Result<DeviceTestResultDto>.Failure("Device not found.");

            var connection = new DeviceConnection(
                device.IpAddress, device.Port, device.CommKey, DeviceConnection.DefaultTimeout);

            var probe = await _client.ProbeAsync(connection, ct);
            if (!probe.IsSuccess)
                return Result<DeviceTestResultDto>.Failure(probe.ErrorMessage!);

            var p = probe.Data!;
            var now = DateTime.Now;

            ApplyProbeToStatus(device, p, now);
            await _uow.Devices.UpdateAsync(device);
            await _uow.SaveChangesAsync();

            var dto = new DeviceTestResultDto
            {
                IsReachable = p.IsReachable,
                ResponseMs = p.ResponseMs,
                Message = p.Message,
                DeviceTime = p.DeviceTime,
                ServerTime = now,
                SerialNumber = p.SerialNumber,
                Model = p.Model
            };

            if (p.DeviceTime.HasValue)
            {
                var drift = (p.DeviceTime.Value - now).TotalSeconds;
                dto.ClockDriftSeconds = Math.Round(drift, 1);
                dto.ClockDriftWarning = Math.Abs(drift) > ClockDriftTolerance.TotalSeconds;
            }

            await _audit.LogAsync(AppConstants.Modules.Devices, "TestConnection",
                _currentUser.UserId, nameof(Device), id);

            return Result<DeviceTestResultDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("DeviceService.TestConnectionAsync", ex);
            return Result<DeviceTestResultDto>.Failure("Failed to test the device connection.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Folds a probe outcome into the device's status.
    ///
    /// A single failure is Offline, not Error: devices are routinely unreachable for a moment
    /// during a network blip, and flagging that as a fault trains operators to ignore the
    /// status column. Error means it has failed repeatedly and needs a person.
    /// </summary>
    private static void ApplyProbeToStatus(Device device, DeviceProbeResult probe, DateTime now)
    {
        if (probe.IsReachable)
        {
            device.Status = DeviceStatus.Online;
            device.LastSeenAt = now;
            device.ConsecutiveFailures = 0;
            device.LastError = null;

            if (!string.IsNullOrWhiteSpace(probe.SerialNumber)) device.SerialNumber = probe.SerialNumber;
            if (!string.IsNullOrWhiteSpace(probe.Model)) device.Model = probe.Model;
        }
        else
        {
            device.ConsecutiveFailures++;
            device.Status = device.ConsecutiveFailures >= ErrorThreshold
                ? DeviceStatus.Error
                : DeviceStatus.Offline;
            device.LastError = probe.Message;
        }
    }

    private async Task<Result> ValidateAsync(SaveDeviceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result.Failure("Device name is required.");

        var ip = dto.IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(ip))
            return Result.Failure("IP address is required.");

        // Accept a hostname or an IP — some sites address devices by DNS name.
        if (!System.Net.IPAddress.TryParse(ip, out _) && ip.Contains(' '))
            return Result.Failure("IP address or hostname is not valid.");

        if (dto.Port is < 1 or > 65535)
            return Result.Failure("Port must be between 1 and 65535.");

        var branch = await _uow.Branches.GetByIdAsync(dto.BranchId);
        if (branch == null)
            return Result.Failure("Selected branch does not exist.");

        // Two rows pointing at one terminal would each download the same punches and each
        // maintain their own watermark. The unique index enforces it; this reports it kindly.
        var sameEndpoint = await _uow.Devices.FindAsync(d =>
            d.IpAddress == ip && d.Port == dto.Port && d.Id != dto.Id && !d.IsDeleted);
        if (sameEndpoint.Any())
            return Result.Failure($"Another device is already registered at {ip}:{dto.Port}.");

        return Result.Success();
    }

    /// <summary>
    /// Maps devices to DTOs, resolving branch names and unmapped-user counts in two queries
    /// rather than two per device.
    /// </summary>
    private async Task<List<DeviceDto>> MapManyAsync(List<Device> devices)
    {
        if (devices.Count == 0) return new List<DeviceDto>();

        var branches = (await _uow.Branches.GetAllAsync()).ToDictionary(b => b.Id, b => b.Name);
        var mappings = await _uow.DeviceUserMappings.FindAsync(m => !m.IsDeleted);
        var mappedPerDevice = mappings
            .GroupBy(m => m.DeviceId)
            .ToDictionary(g => g.Key, g => g.Count());

        return devices.Select(d => new DeviceDto
        {
            Id = d.Id,
            Name = d.Name,
            IpAddress = d.IpAddress,
            Port = d.Port,
            SerialNumber = d.SerialNumber,
            Model = d.Model,
            BranchId = d.BranchId,
            BranchName = branches.TryGetValue(d.BranchId, out var n) ? n : string.Empty,
            IsActive = d.IsActive,
            AutoSyncEnabled = d.AutoSyncEnabled,
            Status = d.Status,
            LastSeenAt = d.LastSeenAt,
            LastSuccessfulSyncAt = d.LastSuccessfulSyncAt,
            LastPunchTimeSynced = d.LastPunchTimeSynced,
            ConsecutiveFailures = d.ConsecutiveFailures,
            LastError = d.LastError,
            HasCommKey = d.CommKey.HasValue && d.CommKey.Value != 0,
            // Phase 3 fills this from the device's user list; until mappings exist it is 0.
            UnmappedUserCount = mappedPerDevice.TryGetValue(d.Id, out var c) ? 0 : 0
        })
        .OrderBy(d => d.BranchName).ThenBy(d => d.Name)
        .ToList();
    }
}

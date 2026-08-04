using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs;

/// <summary>A device as shown in the list and detail screens.</summary>
public class DeviceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Endpoint => $"{IpAddress}:{Port}";

    public string? SerialNumber { get; set; }
    public string? Model { get; set; }

    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public bool AutoSyncEnabled { get; set; }

    public DeviceStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastSuccessfulSyncAt { get; set; }
    public DateTime? LastPunchTimeSynced { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? LastError { get; set; }

    /// <summary>Device users with no employee mapping — punches from them cannot be attributed.</summary>
    public int UnmappedUserCount { get; set; }

    /// <summary>
    /// The comm key is never returned, only whether one is set. Returning it would put a
    /// device credential into every list response.
    /// </summary>
    public bool HasCommKey { get; set; }
}

public class SaveDeviceDto
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 4370;

    /// <summary>Null leaves an existing key unchanged; 0 clears it.</summary>
    public int? CommKey { get; set; }

    [Required]
    public int BranchId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool AutoSyncEnabled { get; set; } = true;
}

/// <summary>Outcome of a connection test — reports the device clock so drift is visible.</summary>
public class DeviceTestResultDto
{
    public bool IsReachable { get; set; }
    public int ResponseMs { get; set; }
    public string? Message { get; set; }

    public DateTime? DeviceTime { get; set; }
    public DateTime ServerTime { get; set; }

    /// <summary>Device clock minus server clock. Drift is the usual cause of "attendance is wrong".</summary>
    public double? ClockDriftSeconds { get; set; }

    /// <summary>True when drift exceeds the tolerance worth telling an operator about.</summary>
    public bool ClockDriftWarning { get; set; }

    public string? SerialNumber { get; set; }
    public string? Model { get; set; }
}

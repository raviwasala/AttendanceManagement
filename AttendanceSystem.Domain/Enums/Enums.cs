namespace AttendanceSystem.Domain.Enums;

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3,
    HalfDay = 4,
    OnLeave = 5,
    Holiday = 6,
    WeeklyOff = 7
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum HolidayType
{
    Public = 1,
    Company = 2
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}

/// <summary>Reachability of a fingerprint device, derived from the last contact attempt.</summary>
public enum DeviceStatus
{
    /// <summary>Never contacted.</summary>
    Unknown = 0,
    Online = 1,
    Offline = 2,
    /// <summary>Repeated failures — needs attention rather than a retry.</summary>
    Error = 3
}

public enum SyncTrigger
{
    Manual = 1,
    Scheduled = 2
}

public enum SyncOutcome
{
    Success = 1,
    /// <summary>Punches downloaded, but some could not be mapped or processed.</summary>
    PartialSuccess = 2,
    Failed = 3
}

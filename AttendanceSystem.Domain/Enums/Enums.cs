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
    /// <summary>Gazetted public holiday — applies to everyone.</summary>
    Public = 1,

    /// <summary>Declared by the company: a shutdown day, an anniversary.</summary>
    Company = 2,

    /// <summary>
    /// A one-off: a declared day of mourning, an election, a local event.
    ///
    /// Treated identically to the others by every calculation — the distinction is for
    /// reporting and for knowing which entries not to carry into next year. Stored as an
    /// int, so adding it needs no migration.
    /// </summary>
    Special = 3
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

/// <summary>
/// Which kind of day an overtime rule applies to. Weekly off and holiday work usually
/// attract a higher multiplier than extra hours on an ordinary working day.
/// </summary>
public enum OvertimeDayType
{
    /// <summary>Matches any day — the fallback rule.</summary>
    Any = 0,
    WorkingDay = 1,
    WeeklyOff = 2,
    Holiday = 3
}

public enum OvertimeStatus
{
    /// <summary>Claimed from attendance, awaiting a decision.</summary>
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

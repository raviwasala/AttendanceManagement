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

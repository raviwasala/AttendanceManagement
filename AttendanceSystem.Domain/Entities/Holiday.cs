using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>Public or company holiday.</summary>
public class Holiday : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime HolidayDate { get; set; }
    public HolidayType HolidayType { get; set; }
    public string? Description { get; set; }
    public bool IsRecurring { get; set; }
}

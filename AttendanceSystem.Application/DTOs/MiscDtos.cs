using AttendanceSystem.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AttendanceSystem.Application.DTOs;

public class HolidayDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>The date this holiday falls on in the year being viewed.</summary>
    public DateTime HolidayDate { get; set; }

    public HolidayType HolidayType { get; set; }
    public string HolidayTypeDisplay => HolidayType.ToString();
    public string? Description { get; set; }
    public bool IsRecurring { get; set; }

    /// <summary>
    /// True when this entry is a recurring holiday shown in a later year than the one it was
    /// declared in. It has no row of its own for that year, so editing or deleting it acts on
    /// the original — which the UI has to say plainly rather than let somebody discover.
    /// </summary>
    public bool IsProjected { get; set; }

    /// <summary>The year the holiday was originally declared. Only interesting when projected.</summary>
    public int DeclaredYear { get; set; }

    public string DateDisplay => HolidayDate.ToString("dd-MMM-yyyy");
    public string DayName => HolidayDate.ToString("dddd");
}

public class SaveHolidayDto
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required] public DateTime HolidayDate { get; set; }
    public HolidayType HolidayType { get; set; } = HolidayType.Public;
    [MaxLength(500)] public string? Description { get; set; }
    public bool IsRecurring { get; set; }
}

public class CompanySettingsDto
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? LogoPath { get; set; }
    public TimeSpan WorkStartTime { get; set; }
    public TimeSpan WorkEndTime { get; set; }
    public string WeekendDays { get; set; } = string.Empty;
    public int MaxLateMinutes { get; set; }
    public int DefaultPageSize { get; set; } = 25;
    public bool ConfirmBeforeDelete { get; set; } = true;
}

public class AuditLogDto
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public int? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedAtDisplay => CreatedAt.ToString("dd-MMM-yyyy hh:mm tt");
}

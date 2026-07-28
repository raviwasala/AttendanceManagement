namespace AttendanceSystem.Domain.Entities;

/// <summary>Company settings / configuration.</summary>
public class CompanySettings : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? LogoPath { get; set; }
    public TimeSpan WorkStartTime { get; set; }
    public TimeSpan WorkEndTime { get; set; }
    public string WeekendDays { get; set; } = "Saturday,Sunday";
    public int MaxLateMinutes { get; set; } = 15;
}

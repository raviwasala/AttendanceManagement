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

    /// <summary>Rows shown per page in every list screen. 0 means "no paging".</summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>
    /// Ask before deleting, and before other irreversible actions. Sites that trust their
    /// operators can switch the prompts off; the default is on.
    /// </summary>
    public bool ConfirmBeforeDelete { get; set; } = true;

    /// <summary>
    /// Minutes of inactivity before the screen locks. 0 disables it.
    ///
    /// Fifteen by default. This system holds salaries, NIC numbers and full staff records on
    /// machines that are often shared, so it should not sit open indefinitely — but a lock
    /// that fires every five minutes gets defeated by the people it is meant to protect,
    /// which is worse than not having one.
    ///
    /// This is not the session timeout. Locking preserves the page and any half-filled form;
    /// the session expiring throws them away. Locking early and expiring late gives security
    /// quickly and data loss slowly.
    /// </summary>
    public int ScreenLockMinutes { get; set; } = 15;
}

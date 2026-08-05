namespace AttendanceSystem.Application.DTOs;

public enum NotificationSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>
/// One thing worth a person's attention right now.
///
/// Derived on request from live data rather than stored: these are all "is this true at the
/// moment?" questions, and a stored notification would need invalidating the instant someone
/// approved the leave or fixed the device.
/// </summary>
public class NotificationDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = "icon-bell";
    public NotificationSeverity Severity { get; set; }

    /// <summary>Where to go to act on it. Null when there is nothing to open.</summary>
    public string? Url { get; set; }

    /// <summary>How many things this represents — shown as a count next to the title.</summary>
    public int Count { get; set; }
}

public class NotificationsDto
{
    public int TotalCount { get; set; }
    public List<NotificationDto> Items { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

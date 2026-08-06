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

    /// <summary>Minutes of inactivity before the screen locks. 0 disables it.</summary>
    public int ScreenLockMinutes { get; set; } = 15;

    // ── Outgoing mail ──────────────────────────────────────────────────────────

    public bool SmtpEnabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;
    public string? SmtpFromAddress { get; set; }
    public string? SmtpFromName { get; set; }

    /// <summary>
    /// Write-only. Sent up to change the password; never populated on the way down.
    ///
    /// Leave it blank when saving and the stored password is kept — otherwise editing any
    /// unrelated setting would wipe the mail password, since the form never had it to send
    /// back.
    /// </summary>
    public string? SmtpPassword { get; set; }

    /// <summary>Read-only signal for the screen: whether a password is stored at all.</summary>
    public bool HasSmtpPassword { get; set; }

    // ── Outgoing SMS ───────────────────────────────────────────────────────────

    public bool SmsEnabled { get; set; }
    public string? SmsProvider { get; set; }
    public string? SmsApiUrl { get; set; }
    public string SmsHttpMethod { get; set; } = "POST";
    public string SmsContentType { get; set; } = "application/json";
    public string? SmsSenderId { get; set; }
    public string? SmsRequestTemplate { get; set; }
    public string? SmsAuthHeader { get; set; }

    /// <summary>Write-only, same rule as the SMTP password: blank on save keeps the stored key.</summary>
    public string? SmsApiKey { get; set; }

    /// <summary>Read-only signal for the screen: whether a key is stored at all.</summary>
    public bool HasSmsApiKey { get; set; }
}

/// <summary>A trial SMS, to prove the gateway settings actually work.</summary>
public class SendTestSmsDto
{
    [Required] public string ToNumber { get; set; } = string.Empty;
}

/// <summary>A trial message, to prove the mail settings actually work.</summary>
public class SendTestEmailDto
{
    [Required, EmailAddress] public string ToEmail { get; set; } = string.Empty;
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

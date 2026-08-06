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

    // ── Outgoing mail ──────────────────────────────────────────────────────────
    // Held here rather than only in appsettings so an administrator can fix a mail
    // problem without server access. Configuration still wins when these are blank,
    // so an existing deployment keeps working untouched.

    /// <summary>Master switch. Off means no mail is attempted and sending reports why.</summary>
    public bool SmtpEnabled { get; set; }

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }

    /// <summary>
    /// The SMTP password, encrypted at rest.
    ///
    /// Encrypted rather than stored plainly because <c>Settings.View</c> is a far weaker
    /// permission than "may read the company's mail credentials", and a database backup
    /// travels further than the database does. It is never returned to the browser — the
    /// screen receives only a flag saying whether one is set, the same discipline the
    /// remember-me and reset tokens already follow.
    /// </summary>
    public string? SmtpPasswordEncrypted { get; set; }

    public bool SmtpEnableSsl { get; set; } = true;

    /// <summary>Address mail is sent from. Falls back to the username when blank.</summary>
    public string? SmtpFromAddress { get; set; }

    /// <summary>Display name on outgoing mail.</summary>
    public string? SmtpFromName { get; set; }

    // ── Outgoing SMS ───────────────────────────────────────────────────────────
    // Deliberately generic rather than modelled on one vendor. The gateways used in this
    // market — Text.lk, Notify.lk, Dialog, Mobitel — are all plain HTTP with different
    // shapes, and hard-coding any one of them would make switching a code change.

    /// <summary>Master switch. Off means no SMS is attempted and sending reports why.</summary>
    public bool SmsEnabled { get; set; }

    /// <summary>Free-text label for whoever maintains this, e.g. "Text.lk". Not behaviour.</summary>
    public string? SmsProvider { get; set; }

    /// <summary>Endpoint the request is sent to.</summary>
    public string? SmsApiUrl { get; set; }

    /// <summary>GET or POST.</summary>
    public string SmsHttpMethod { get; set; } = "POST";

    /// <summary><c>application/json</c> or <c>application/x-www-form-urlencoded</c>.</summary>
    public string SmsContentType { get; set; } = "application/json";

    /// <summary>API key or token, encrypted at rest — same reasoning as the SMTP password.</summary>
    public string? SmsApiKeyEncrypted { get; set; }

    /// <summary>Registered sender name or short code, where the gateway requires one.</summary>
    public string? SmsSenderId { get; set; }

    /// <summary>
    /// Request body (POST) or query string (GET), with placeholders substituted at send time:
    /// <c>{to}</c>, <c>{message}</c>, <c>{apikey}</c>, <c>{sender}</c>.
    ///
    /// A template rather than fixed fields because no two gateways agree on parameter names.
    /// </summary>
    public string? SmsRequestTemplate { get; set; }

    /// <summary>
    /// Optional <c>Authorization</c> header value, e.g. <c>Bearer {apikey}</c>. Gateways split
    /// roughly evenly between header auth and putting the key in the body.
    /// </summary>
    public string? SmsAuthHeader { get; set; }
}

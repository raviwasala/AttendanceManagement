namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Whether one dashboard widget is shown, for one user — or for everyone.
///
/// <see cref="UserId"/> null is the company default that new users start from, rather than a
/// second table holding the same three columns. That also makes "reset to the default" a
/// delete of the user's own rows, with nothing to copy.
///
/// Absence means "use the default": a user with no rows sees the company default, and a
/// company with no rows sees the defaults defined in code. Nobody has to configure anything
/// before the dashboard works.
/// </summary>
public class DashboardPreference : BaseEntity
{
    /// <summary>The user this applies to. Null is the company-wide default.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// Matches a key in the code-defined widget catalogue. Rows whose key is no longer in the
    /// catalogue are ignored, so removing a widget in a later version cannot break a dashboard
    /// that still has a preference for it.
    /// </summary>
    public string WidgetKey { get; set; } = string.Empty;

    public bool IsVisible { get; set; } = true;
}

using AttendanceSystem.Common.Constants;

namespace AttendanceSystem.Application.DTOs;

/// <summary>
/// One entry in the dashboard widget catalogue.
///
/// The catalogue lives in code rather than the database. A widget is markup plus a data
/// endpoint plus a permission — three things that ship together — so a row describing one that
/// no longer exists would be a promise the application cannot keep. Preferences reference these
/// by key, and unknown keys are ignored.
/// </summary>
public class DashboardWidgetDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Permission needed to see it at all. Offering a widget somebody cannot load is worse than hiding it.</summary>
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;

    /// <summary>Whether it is currently shown for whoever asked.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Shown when the user has not chosen and is following the default.</summary>
    public bool IsDefault { get; set; }
}

public class SaveDashboardPreferencesDto
{
    /// <summary>Keys the user wants visible. Anything absent is hidden.</summary>
    public List<string> VisibleKeys { get; set; } = new();
}

/// <summary>
/// The dashboard widget catalogue.
///
/// Order here is the order on screen: reordering is deliberately not offered, so this list is
/// the single place layout is decided and it cannot drift from the markup.
/// </summary>
public static class DashboardWidgetCatalogue
{
    public static readonly IReadOnlyList<DashboardWidgetDto> All =
    [
        // Employees.View, not Dashboard.View. These are company headcount figures, and
        // Dashboard.View is held by every role including plain employees — so classifying
        // them as "dashboard" showed the whole company's daily attendance to everyone who
        // could log in. The permission has to describe the data, not the page it sits on.
        new() { Key = "stats", Title = "Summary tiles",
                Description = "Total employees, present, absent and on leave today.",
                Module = AppConstants.Modules.Employees, Action = AppConstants.Actions.View,
                IsDefault = true },

        new() { Key = "trend", Title = "Attendance trend",
                Description = "Present, absent and late over recent days.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View,
                IsDefault = true },

        new() { Key = "punctuality", Title = "Punctuality",
                Description = "Late percentage, average lateness and the pattern by weekday.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View,
                IsDefault = true },

        new() { Key = "toplate", Title = "Most late arrivals",
                Description = "The employees arriving late most often.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View,
                IsDefault = true },

        new() { Key = "pendingleave", Title = "Pending leave",
                Description = "Requests waiting for approval.",
                Module = AppConstants.Modules.Leave, Action = AppConstants.Actions.View,
                IsDefault = true },

        new() { Key = "leaveutil", Title = "Leave utilisation",
                Description = "How much of each leave type has been used this year.",
                Module = AppConstants.Modules.Leave, Action = AppConstants.Actions.View,
                IsDefault = true },

        new() { Key = "datahealth", Title = "Data health",
                Description = "Employees missing a biometric enrol ID, days with no check-out, and similar gaps.",
                Module = AppConstants.Modules.Employees, Action = AppConstants.Actions.View,
                IsDefault = true },

        // Today's percentage, the breakdown chart and the recent-attendance table. This block
        // was carrying the "quicklinks" key, so a named list of who was present, absent and
        // late today was gated on Dashboard.View along with the harmless shortcuts.
        new() { Key = "todayattendance", Title = "Today's attendance",
                Description = "Attendance percentage, the present/absent/late breakdown and today's recent punches.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View,
                IsDefault = true },

        // Genuinely just links. Every destination gates itself, so a shortcut to a screen the
        // user cannot open is harmless — it stops at that screen's own permission check.
        new() { Key = "quicklinks", Title = "Quick links",
                Description = "Shortcuts to the screens used most often.",
                Module = AppConstants.Modules.Dashboard, Action = AppConstants.Actions.View,
                IsDefault = true }
    ];

    public static bool IsKnown(string key) =>
        All.Any(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase));
}

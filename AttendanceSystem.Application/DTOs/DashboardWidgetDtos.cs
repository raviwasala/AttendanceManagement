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
        new() { Key = "stats", Title = "Summary tiles",
                Description = "Total employees, present, absent and on leave today.",
                Module = AppConstants.Modules.Dashboard, Action = AppConstants.Actions.View,
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

        new() { Key = "quicklinks", Title = "Quick links",
                Description = "Shortcuts to the screens used most often.",
                Module = AppConstants.Modules.Dashboard, Action = AppConstants.Actions.View,
                IsDefault = true }
    ];

    public static bool IsKnown(string key) =>
        All.Any(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase));
}

using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Common.Constants;

namespace AttendanceSystem.Application.DTOs;

/// <summary>
/// A metric a custom tile can show.
///
/// The catalogue is code, and each entry carries the permission needed to see it. That is what
/// makes user-composed tiles safe: a user can only assemble a tile from numbers they are
/// already allowed to read, and the number itself is produced by an evaluator here rather than
/// by anything they wrote.
/// </summary>
public class DashboardMetricDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;

    /// <summary>False for metrics that are a state right now, so the period picker is hidden.</summary>
    public bool SupportsPeriod { get; set; } = true;

    /// <summary>Appended to the value — "h" for hours. Empty for plain counts.</summary>
    public string Suffix { get; set; } = string.Empty;
}

public static class DashboardMetricCatalogue
{
    public static readonly IReadOnlyList<DashboardMetricDto> All =
    [
        new() { Key = "present",  Title = "Present",  Description = "Days recorded as present.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View },
        new() { Key = "late",     Title = "Late",     Description = "Days someone arrived late.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View },
        new() { Key = "absent",   Title = "Absent",   Description = "Days recorded as absent.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View },
        new() { Key = "onleave",  Title = "On leave", Description = "Days recorded as on leave.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View },
        new() { Key = "nocheckout", Title = "Missing check-out",
                Description = "Days with a check-in but no check-out.",
                Module = AppConstants.Modules.Attendance, Action = AppConstants.Actions.View },

        new() { Key = "othours",  Title = "Overtime hours", Suffix = "h",
                Description = "Approved overtime, in hours.",
                Module = AppConstants.Modules.Overtime, Action = AppConstants.Actions.View },
        new() { Key = "otpending", Title = "Overtime awaiting approval", SupportsPeriod = false,
                Description = "Overtime records still pending, whenever they were earned.",
                Module = AppConstants.Modules.Overtime, Action = AppConstants.Actions.View },

        new() { Key = "leavepending", Title = "Leave awaiting approval", SupportsPeriod = false,
                Description = "Leave requests still pending.",
                Module = AppConstants.Modules.Leave, Action = AppConstants.Actions.View },

        new() { Key = "headcount", Title = "Headcount", SupportsPeriod = false,
                Description = "Active employees.",
                Module = AppConstants.Modules.Employees, Action = AppConstants.Actions.View },
        new() { Key = "missingenroll", Title = "Missing enrol ID", SupportsPeriod = false,
                Description = "Active employees with no biometric enrol ID — these can never be matched by an import.",
                Module = AppConstants.Modules.Employees, Action = AppConstants.Actions.View }
    ];

    public static DashboardMetricDto? Find(string? key) =>
        All.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
}

public class DashboardTileDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MetricKey { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public int? BranchId { get; set; }
    public string Period { get; set; } = "today";
    public string Colour { get; set; } = "bg-c-blue";
    public int SortOrder { get; set; }

    /// <summary>The computed number. Null when the metric is no longer in the catalogue.</summary>
    public double? Value { get; set; }
    public string Suffix { get; set; } = string.Empty;

    /// <summary>"Production · this month", so a tile explains itself without being opened.</summary>
    public string ScopeDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Where the tile drills through to, carrying its own scope and period as query
    /// parameters. Built on the server so the mapping from metric to screen lives next to the
    /// metric definition rather than being duplicated in JavaScript.
    /// </summary>
    public string? Url { get; set; }
}

public class SaveDashboardTileDto
{
    public int Id { get; set; }

    [Required, MaxLength(60)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string MetricKey { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }
    public int? BranchId { get; set; }

    [MaxLength(20)] public string Period { get; set; } = "today";
    [MaxLength(30)] public string Colour { get; set; } = "bg-c-blue";
}

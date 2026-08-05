namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A KPI tile a user composed for their own dashboard.
///
/// Deliberately not a saved query. The tile stores *which* metric, *which* scope and *which*
/// period — all chosen from fixed lists — and the number is computed by code. A stored query
/// would let a tile read data its owner is not permitted to see, and would put an unreviewed
/// shape of SQL in front of a table with six figures of attendance rows.
/// </summary>
public class UserDashboardTile : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>The user's own wording — "Production late this month".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Key into the code-defined metric catalogue. Unknown keys are ignored.</summary>
    public string MetricKey { get; set; } = string.Empty;

    /// <summary>Null means every department / branch.</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Key into the period list: today, week, month, lastmonth.</summary>
    public string Period { get; set; } = "today";

    /// <summary>One of the theme's stat-card classes, so custom tiles match the standard ones.</summary>
    public string Colour { get; set; } = "bg-c-blue";

    public int SortOrder { get; set; }
}

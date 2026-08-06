namespace AttendanceSystem.Application.Services;

/// <summary>
/// Whose records a user may read.
///
/// The permission model answers "may this person open the employee list"; it cannot answer
/// "…and whose rows are in it", because a permission is a capability with no scope attached.
/// That is the same gap <see cref="LeaveApprovalScope"/> fills for approvals, and this is its
/// counterpart for reading — deliberately built from the same configuration so a manager does
/// not end up approving for one set of departments and seeing a different one.
///
/// Three shapes, in widening order:
///
///   1. <b>Self only</b> — no <c>Employees.View</c>. An ordinary employee sees their own
///      records and nobody else's.
///   2. <b>Assigned departments</b> — <c>Employees.View</c> and a narrowed
///      <see cref="Domain.Enums.ApprovalScope"/>. Sees the departments they head or are a
///      named approver of.
///   3. <b>Company-wide</b> — <c>Employees.View</c> and company-wide approval scope.
///      Administrators and HR.
/// </summary>
public sealed class DataScope
{
    /// <summary>True when this user may read every employee's records.</summary>
    public bool IsCompanyWide { get; }

    /// <summary>Departments readable when not company-wide.</summary>
    public IReadOnlySet<int> DepartmentIds { get; }

    /// <summary>The employee behind this user, when they have one.</summary>
    public int? OwnEmployeeId { get; }

    public DataScope(bool isCompanyWide, IReadOnlySet<int> departmentIds, int? ownEmployeeId)
    {
        IsCompanyWide = isCompanyWide;
        DepartmentIds = departmentIds;
        OwnEmployeeId = ownEmployeeId;
    }

    /// <summary>
    /// True when this user sees only themselves — no company-wide reach and no departments.
    /// </summary>
    public bool IsSelfOnly => !IsCompanyWide && DepartmentIds.Count == 0;

    /// <summary>Whether one employee's records are readable under this scope.</summary>
    public bool Allows(int employeeId, int departmentId) =>
        IsCompanyWide
        || DepartmentIds.Contains(departmentId)
        || (OwnEmployeeId.HasValue && OwnEmployeeId.Value == employeeId);

    /// <summary>
    /// The department ids to filter a query by, or null when no department filter applies —
    /// either because everything is visible or because the scope is self-only and
    /// <see cref="OwnEmployeeId"/> is the filter instead.
    ///
    /// Returned as a list so repositories can translate it straight into a SQL <c>IN</c>
    /// rather than each caller deciding how to express the same restriction.
    /// </summary>
    public IReadOnlyCollection<int>? DepartmentFilter =>
        IsCompanyWide || DepartmentIds.Count == 0 ? null : DepartmentIds.ToList();

    /// <summary>
    /// The single employee id a query must be restricted to, or null when the scope is wider.
    /// </summary>
    public int? EmployeeFilter => IsSelfOnly ? (OwnEmployeeId ?? -1) : null;

    /// <summary>
    /// A scope that can read nothing. Used when there is no signed-in user, so a missing
    /// session fails closed rather than falling through to company-wide.
    /// </summary>
    public static DataScope Nothing { get; } =
        new(false, new HashSet<int>(), ownEmployeeId: -1);
}

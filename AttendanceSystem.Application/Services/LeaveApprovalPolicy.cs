using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Who may approve whose leave or overtime.
///
/// The permission <c>Leave.Approve</c> answers "may this person approve at all". This answers
/// "…and for whom", which the permission model cannot express: a permission is a capability
/// with no scope attached, and department-scoped approval needs capability × scope.
///
/// Two rules:
///
///   1. <b>Nobody decides their own request.</b> Holding an approve permission and applying
///      would otherwise let one person do both halves of the decision.
///
///   2. <b>Reach comes from <see cref="ApprovalScope"/> on the user.</b> CompanyWide reaches
///      every department; AssignedDepartments reaches only those they head or are named an
///      approver of.
///
/// Rule 2 used to be inferred — "named nowhere means unrestricted" — which made company-wide
/// approval invisible on every screen and meant naming somebody for one department silently
/// stripped their reach everywhere else. Stating it on the user makes both directions
/// deliberate: adding an HR Manager to a department is now a no-op rather than a demotion.
/// </summary>
public sealed class LeaveApprovalScope
{
    /// <summary>True when this user approves for every department.</summary>
    public bool IsCompanyWide { get; }

    /// <summary>Departments reached when not company-wide.</summary>
    public IReadOnlySet<int> DepartmentIds { get; }

    /// <summary>The employee record behind the approving user, when they have one.</summary>
    public int? OwnEmployeeId { get; }

    public LeaveApprovalScope(bool isCompanyWide, IReadOnlySet<int> departmentIds, int? ownEmployeeId)
    {
        IsCompanyWide = isCompanyWide;
        DepartmentIds = departmentIds;
        OwnEmployeeId = ownEmployeeId;
    }

    /// <summary>
    /// Whether this approver may act on a request. <paramref name="requestEmployeeId"/> and
    /// <paramref name="requestDepartmentId"/> describe the person who applied.
    ///
    /// <paramref name="subject"/> names what is being decided ("leave request", "overtime
    /// claim") so the refusal reads naturally wherever it came from.
    /// </summary>
    public bool CanApprove(int requestEmployeeId, int requestDepartmentId, out string? reason,
                           string subject = "leave request")
    {
        // Checked before scope: being company-wide does not entitle anyone to sign off their
        // own request, and that is the rule most likely to be reached for.
        if (OwnEmployeeId.HasValue && OwnEmployeeId.Value == requestEmployeeId)
        {
            reason = $"You cannot approve your own {subject}. Someone else must decide it.";
            return false;
        }

        if (IsCompanyWide) { reason = null; return true; }

        // A real state, not a misconfiguration to paper over: somebody restricted to assigned
        // departments who has not been given any approves nothing, and saying so plainly is
        // how it gets noticed and fixed.
        if (DepartmentIds.Count == 0)
        {
            reason = "You are not set as head or approver of any department, so there is " +
                     $"nothing you can decide. Ask an administrator to assign your departments.";
            return false;
        }

        if (!DepartmentIds.Contains(requestDepartmentId))
        {
            reason = $"You can only decide {subject}s for the departments you are responsible for.";
            return false;
        }

        reason = null;
        return true;
    }
}

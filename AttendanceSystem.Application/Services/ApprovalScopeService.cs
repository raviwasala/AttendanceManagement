using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Builds a <see cref="LeaveApprovalScope"/> for a user.
///
/// Shared by leave and overtime deliberately. Both answer the same question — "may this person
/// decide this request, for this employee" — and two copies of that rule would eventually
/// disagree, which is exactly the drift the single <c>AttendanceCalculator</c> exists to avoid
/// on the arithmetic side.
/// </summary>
public class ApprovalScopeService : IApprovalScopeService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _currentUser;

    public ApprovalScopeService(IUnitOfWork uow, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public Task<LeaveApprovalScope> GetForCurrentUserAsync() =>
        GetForAsync(_currentUser.UserId ?? 0);

    public async Task<LeaveApprovalScope> GetForAsync(int userId)
    {
        var user = userId > 0 ? await _uow.Users.GetByIdAsync(userId) : null;
        var ownEmployeeId = user?.EmployeeId;

        // Read from the user, not inferred from whether they appear in any department. A
        // company-wide approver stays company-wide even if somebody adds them to a
        // department — which is what stops "add the HR Manager here" quietly demoting them.
        var isCompanyWide = user?.ApprovalScope != Domain.Enums.ApprovalScope.AssignedDepartments;

        var departments = new HashSet<int>();

        if (!isCompanyWide)
        {
            // Heading a department is recorded against the employee, so it only reaches
            // approval when that employee also has a login.
            if (ownEmployeeId.HasValue)
            {
                foreach (var d in await _uow.Departments.FindAsync(
                             d => d.HeadEmployeeId == ownEmployeeId && !d.IsDeleted))
                {
                    departments.Add(d.Id);
                }
            }

            foreach (var a in await _uow.DepartmentApprovers.FindAsync(a => a.UserId == userId && !a.IsDeleted))
                departments.Add(a.DepartmentId);
        }

        return new LeaveApprovalScope(isCompanyWide, departments, ownEmployeeId);
    }
}

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A user named as an approver for one department.
///
/// Sits alongside <see cref="Department.HeadEmployeeId"/> rather than replacing it: the head is
/// who runs the department, which is a fact about the organisation, while an approver is who
/// may sign off leave, which is a permission. They are usually the same person and sometimes
/// deliberately are not — a head on long leave, or a department where two supervisors share
/// the duty.
///
/// The rule that makes this safe to deploy: <b>being named anywhere restricts you to those
/// departments; being named nowhere leaves you unrestricted.</b> So an HR Manager who is not
/// assigned to any department keeps approving for the whole company exactly as before, and
/// naming a supervisor for Production narrows that supervisor rather than widening them.
/// Nobody loses access the day this ships.
/// </summary>
public class DepartmentApprover : BaseEntity
{
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    /// <summary>
    /// The user, not the employee. Approving is something an account does, and an approver
    /// without a login could never act on the request.
    /// </summary>
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}

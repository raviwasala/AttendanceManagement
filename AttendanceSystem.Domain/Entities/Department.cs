namespace AttendanceSystem.Domain.Entities;

/// <summary>Employee department.</summary>
public class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Who runs this department. Optional — many sites never fill it in, and approval has to
    /// keep working when it is null.
    ///
    /// An employee rather than a user, because heading a department is a fact about the
    /// organisation. Whether that person can *act* on approvals additionally requires a linked
    /// account and the Leave.Approve permission.
    /// </summary>
    public int? HeadEmployeeId { get; set; }
    public Employee? HeadEmployee { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();

    /// <summary>Users allowed to approve for this department, in addition to the head.</summary>
    public ICollection<DepartmentApprover> Approvers { get; set; } = new List<DepartmentApprover>();
}

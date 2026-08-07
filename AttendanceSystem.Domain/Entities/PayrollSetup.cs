namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A bank, for salary transfers.
///
/// <see cref="Code"/> is the bank's SLIPS code — the identifier the transfer file is keyed on,
/// not a name. Two banks can share a trading name across a merger; the code is what the
/// clearing system matches.
/// </summary>
public class Bank : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>SLIPS bank code, e.g. 7010 for Bank of Ceylon.</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<BankBranch> Branches { get; set; } = new List<BankBranch>();
}

/// <summary>
/// A branch of a bank. Salary transfers need bank code *and* branch code, so the account
/// alone is not enough to pay somebody.
/// </summary>
public class BankBranch : BaseEntity
{
    public int BankId { get; set; }
    public Bank Bank { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>SLIPS branch code.</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A salary grade — <b>the single source of basic salary</b>.
///
/// Basic lives here rather than on the employee because the grade is the policy: raising a
/// grade raises everyone on it, and an employee-level basic would let the two disagree with
/// no way to tell which was intended. An individual exception is expressed as an allowance,
/// which is visible on the payslip, rather than as a quiet difference in basic.
/// </summary>
public class SalaryGrade : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>Monthly basic salary for this grade.</summary>
    public decimal BasicSalary { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<EmployeePayrollInfo> Employees { get; set; } = new List<EmployeePayrollInfo>();
}

/// <summary>
/// A salary group — Executive, Non-Executive, Casual and so on.
///
/// Reporting and eligibility only: it does not carry money. Grouping and grade are separate
/// because two people on the same grade can sit in different groups, and conflating them
/// would force a grade per combination.
/// </summary>
public class SalaryGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A subdivision of a department — "Bakery → Production", "Bakery → Packing".
///
/// One level deep on purpose. An arbitrary tree reads well on a whiteboard and then has to be
/// flattened for every report and every payroll summary; the sites this serves group two
/// levels and no more.
/// </summary>
public class SubDepartment : BaseEntity
{
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

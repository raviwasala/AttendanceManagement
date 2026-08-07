namespace AttendanceSystem.Domain.Entities;

/// <summary>Company branch / location.</summary>
public class Branch : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The employer's EPF registration number for this branch, printed on the C-form.
    ///
    /// Held per branch, not company-wide: a group commonly registers each location
    /// separately, and returns are filed against the registration the employee's
    /// contributions were made under. One shared number would produce a return that the
    /// fund rejects.
    /// </summary>
    public string? EpfEmployerNumber { get; set; }

    /// <summary>The employer's ETF registration number for this branch, for the R-4 return.</summary>
    public string? EtfEmployerNumber { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

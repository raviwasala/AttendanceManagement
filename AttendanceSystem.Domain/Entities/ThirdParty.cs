namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// An outside organisation that money deducted from salaries is paid over to — an insurance
/// company, a union, a finance company.
///
/// Exists so a deduction has a destination. Without it the payroll knows what was taken off
/// each employee but not who it is owed to, and the remittance becomes a spreadsheet somebody
/// maintains by hand.
/// </summary>
public class ThirdParty : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Where the remittance and its schedule are sent.</summary>
    public string? Address { get; set; }

    /// <summary>
    /// The deduction this party receives.
    ///
    /// A link to a <see cref="SalaryComponent"/> rather than a code typed twice: the component
    /// already defines the deduction, and duplicating its code here would let the two drift
    /// until a remittance was raised against a deduction nobody was taking.
    ///
    /// Nullable because a payee can be recorded before anyone decides which deduction feeds it.
    /// </summary>
    public int? SalaryComponentId { get; set; }
    public SalaryComponent? SalaryComponent { get; set; }

    public bool IsActive { get; set; } = true;
}

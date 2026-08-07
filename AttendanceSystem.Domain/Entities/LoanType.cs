using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A kind of staff loan — its interest model and default rate.
///
/// The rate lives here as a default rather than as the rate: an individual loan records the
/// rate it was actually granted at, so changing this later cannot restate what somebody
/// already owes. That separation is the whole reason this is a type rather than a setting.
/// </summary>
public class LoanType : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Flat or reducing balance. Recorded per type because the same headline rate produces
    /// materially different totals under each, and a borrower will ask which was used.
    /// </summary>
    public LoanInterestType InterestType { get; set; } = LoanInterestType.Fixed;

    /// <summary>
    /// Annual interest rate as a percentage. Zero is normal — most staff loans are
    /// interest-free, which is why it is not required.
    /// </summary>
    public decimal InterestRate { get; set; }

    public bool IsActive { get; set; } = true;
}

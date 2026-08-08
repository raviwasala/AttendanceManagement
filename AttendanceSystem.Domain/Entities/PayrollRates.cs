using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// EPF and ETF contribution rates, with the date they took effect.
///
/// Data rather than constants, and versioned by date rather than edited in place. The rates
/// have been 8/12/3 for a long time, but a payslip regenerated for an earlier month must use
/// the rates of that month — hard-coding them would silently restate history the first time
/// they changed.
/// </summary>
public class EpfEtfRate : BaseEntity
{
    public DateTime EffectiveFrom { get; set; }

    /// <summary>Employee's EPF contribution, deducted from pay. Statutory minimum 8%.</summary>
    public decimal EmployeeEpfPercent { get; set; } = 8m;

    /// <summary>Employer's EPF contribution, paid on top of pay. Statutory minimum 12%.</summary>
    public decimal EmployerEpfPercent { get; set; } = 12m;

    /// <summary>Employer's ETF contribution. Statutory 3%, with no employee share.</summary>
    public decimal EmployerEtfPercent { get; set; } = 3m;

    public string? Notes { get; set; }
}

/// <summary>
/// A named APIT table.
///
/// More than one is in force at a time — primary and secondary employment are taxed on
/// different tables, and each employee is assigned the one that applies to them. Modelling
/// bands as a single global list would force every employee onto the same rates, which is
/// wrong for anyone with a second job.
/// </summary>
public class ApitTaxTable : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Short identifier, matching how the IRD numbers its tables.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Which IRD schedule this is — monthly, bonus, non-citizen, yearly, tax-on-tax.
    ///
    /// The payroll picks a table by type, not by name: asking for "the bonus table" has to
    /// keep working when somebody renames it or supersedes it at the next budget.
    /// </summary>
    public TaxTableType TableType { get; set; } = TaxTableType.Monthly;

    /// <summary>
    /// Used for employees who have not been assigned one explicitly.
    ///
    /// Default is per <see cref="TableType"/>, not across all tables: every type needs its own
    /// fallback, and one global default would leave four of the five with nothing to fall
    /// back to.
    /// </summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ApitTaxBracket> Brackets { get; set; } = new List<ApitTaxBracket>();
}

/// <summary>
/// Employment type — Permanent, Contract, Casual, Probation.
///
/// Drives eligibility rather than money: EPF membership, gratuity and leave entitlement
/// commonly hang off it, while what somebody is paid comes from their grade.
/// </summary>
public class EmploymentCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>Whether staff in this category join EPF and ETF by default.</summary>
    public bool IsEpfEligible { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One band of an APIT (PAYE) table.
///
/// Sri Lankan APIT is a slab table applied to monthly regular profits: tax is
/// <c>earnings × Rate − Relief</c> for the band the earnings fall in. Holding
/// <see cref="Relief"/> per band rather than computing cumulative tax across bands keeps the
/// arithmetic to one line and matches how the published tables are written, so a clerk can
/// check a payslip against the gazette without reverse-engineering anything.
///
/// Versioned by <see cref="EffectiveFrom"/> because the bands change with the budget, and a
/// reissued payslip must use the table of its own month.
/// </summary>
public class ApitTaxBracket : BaseEntity
{
    /// <summary>Which table this band belongs to.</summary>
    public int ApitTaxTableId { get; set; }
    public ApitTaxTable ApitTaxTable { get; set; } = null!;

    public DateTime EffectiveFrom { get; set; }

    /// <summary>Lower bound of the band, inclusive.</summary>
    public decimal FromAmount { get; set; }

    /// <summary>Upper bound, exclusive. Null for the top band, which is open-ended.</summary>
    public decimal? ToAmount { get; set; }

    /// <summary>Marginal rate for the band, as a percentage.</summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Subtracted after applying the rate — the published tables' constant that makes the
    /// slab arithmetic continuous at the band boundaries.
    /// </summary>
    public decimal Relief { get; set; }

    public int SortOrder { get; set; }
}

using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// A line that can appear on a payslip — an allowance, or a deduction.
///
/// The flags are the whole point. Sri Lankan payroll asks several independent questions of
/// every component, and they genuinely do not move together — an allowance can be taxable
/// but outside EPF, or counted for overtime but not for no-pay:
///
///   • <see cref="Recurrence"/> — monthly, or entered for a single month.
///   • <see cref="IsEpfLiable"/> — enters EPF and ETF earnings.
///   • <see cref="IsApitLiable"/> — enters APIT (PAYE) earnings. Broader than EPF.
///   • <see cref="IncludeInOtRate"/> — enters the base the overtime hourly rate is built from.
///   • <see cref="IncludeInGrossPay"/> — counts as earnings rather than a reimbursement.
///   • <see cref="IncludeInNoPay"/> — enters the no-pay day rate alongside basic.
///   • <see cref="IncludeInAllowanceOnlyNoPay"/> — enters the second no-pay basis, computed
///     from allowances alone.
///   • <see cref="BasedOnWorkingDays"/> — the rate is per working day, not per month.
///
/// Encoding these as one "type" would force a component to be all or none, and every site
/// would then need code changes for its own allowance list.
/// </summary>
public class SalaryComponent : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code shown on the payslip and used in the export, e.g. BRA, TRA.</summary>
    public string Code { get; set; } = string.Empty;

    public SalaryComponentType ComponentType { get; set; } = SalaryComponentType.Earning;

    /// <summary>
    /// Whether this repeats monthly or is entered for a single month.
    ///
    /// Was a boolean "IsFixed" that also stood in for EPF liability. Splitting them was
    /// necessary: recurrence and EPF liability genuinely differ per allowance, and inferring
    /// one from the other quietly charged EPF on the wrong set.
    /// </summary>
    public ComponentRecurrence Recurrence { get; set; } = ComponentRecurrence.Monthly;

    /// <summary>Counts toward EPF-liable earnings.</summary>
    public bool IsEpfLiable { get; set; }

    /// <summary>Counts toward APIT (PAYE) taxable earnings.</summary>
    public bool IsApitLiable { get; set; } = true;

    /// <summary>
    /// Counts toward the earnings the overtime hourly rate is derived from.
    ///
    /// Separate from every other flag: an allowance can be paid, taxed and EPF-liable and
    /// still be excluded from the overtime base, or the reverse. Sri Lankan practice is that
    /// the OT rate is built from basic plus specifically nominated allowances, not from gross.
    /// </summary>
    public bool IncludeInOtRate { get; set; }

    /// <summary>
    /// Counts toward the gross pay figure shown and reported.
    ///
    /// Almost always true. False for reimbursements that pass through the payslip because
    /// that is where the money is paid, but are not earnings — so including them would
    /// overstate what the person was paid.
    /// </summary>
    public bool IncludeInGrossPay { get; set; } = true;

    /// <summary>
    /// The rate is a per-working-day figure rather than a monthly one, so the month's amount
    /// is rate × working days.
    ///
    /// Distinct from pro-rating: pro-rating reduces a monthly figure for days not worked,
    /// while this builds the figure up from days in the first place. A month with more
    /// working days pays more under this rule and the same under the other.
    /// </summary>
    public bool BasedOnWorkingDays { get; set; }

    public ComponentCalculationType CalculationType { get; set; } = ComponentCalculationType.FixedAmount;

    /// <summary>Amount, or percentage of basic, depending on <see cref="CalculationType"/>.</summary>
    public decimal DefaultValue { get; set; }

    /// <summary>
    /// Counts toward the earnings the no-pay day rate is derived from, alongside basic.
    ///
    /// False for things earned by turning up at all rather than by the day — a travel
    /// allowance is usually paid whole, while a cost-of-living allowance is usually reduced.
    /// Getting this wrong is silent: the payslip simply comes out slightly wrong.
    /// </summary>
    public bool IncludeInNoPay { get; set; } = true;

    /// <summary>
    /// Belongs to the second no-pay basis, where the day rate comes from allowances alone
    /// rather than from basic plus allowances.
    ///
    /// A separate pool rather than a variation of <see cref="IncludeInNoPay"/>: the two
    /// produce different day rates and are applied to different staff, so a component has to
    /// be able to sit in one, both, or neither.
    /// </summary>
    public bool IncludeInAllowanceOnlyNoPay { get; set; }

    /// <summary>Order on the payslip, so the layout is deliberate rather than by id.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One employee's value for a component, where it differs from the component default.
///
/// Absent means "use the default". Storing a row per employee per component regardless would
/// make a change to the default invisible — every employee would keep a copy of the old value.
/// </summary>
public class EmployeeSalaryComponent : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    public decimal Value { get; set; }

    /// <summary>
    /// When this override applies from and until. A pay revision is a new row rather than an
    /// edit, so a payslip re-generated for an earlier month still uses the value that was in
    /// force then.
    /// </summary>
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

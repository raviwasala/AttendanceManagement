using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs;

// ── Banks ─────────────────────────────────────────────────────────────────────

public class BankDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>How many branches are on file — shown so an empty bank is obvious.</summary>
    public int BranchCount { get; set; }
}

public class SaveBankDto
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class BankBranchDto
{
    public int Id { get; set; }
    public int BankId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Bank and branch code together — what the transfer file is keyed on.</summary>
    public string FullCode { get; set; } = string.Empty;
}

public class SaveBankBranchDto
{
    public int Id { get; set; }
    [Required] public int BankId { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

// ── Grades and groups ─────────────────────────────────────────────────────────

public class SalaryGradeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Employees on this grade. Raising the basic moves all of them.</summary>
    public int EmployeeCount { get; set; }
}

public class SaveSalaryGradeDto
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Range(0, 99999999)] public decimal BasicSalary { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SalaryGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int EmployeeCount { get; set; }
}

public class SaveSalaryGroupDto
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Sub-departments ───────────────────────────────────────────────────────────

public class SubDepartmentDto
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int EmployeeCount { get; set; }
}

public class SaveSubDepartmentDto
{
    public int Id { get; set; }
    [Required] public int DepartmentId { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

// ── Salary components ─────────────────────────────────────────────────────────

public class SalaryComponentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; }
    public string ComponentTypeDisplay => ComponentType == SalaryComponentType.Earning ? "Earning" : "Deduction";

    public ComponentRecurrence Recurrence { get; set; }
    public string RecurrenceDisplay => Recurrence == ComponentRecurrence.Monthly ? "Monthly" : "One-off";

    public bool IsEpfLiable { get; set; }
    public bool IsApitLiable { get; set; }
    public bool IncludeInOtRate { get; set; }
    public bool IncludeInGrossPay { get; set; }
    public bool BasedOnWorkingDays { get; set; }
    public bool IncludeInNoPay { get; set; }
    public bool IncludeInAllowanceOnlyNoPay { get; set; }

    public ComponentCalculationType CalculationType { get; set; }
    public decimal DefaultValue { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>The value as it reads — "Rs 5,000.00" or "10% of basic".</summary>
    public string ValueDisplay =>
        CalculationType == ComponentCalculationType.PercentOfBasic
            ? $"{DefaultValue:0.##}% of basic"
            : DefaultValue.ToString("N2");
}

public class SaveSalaryComponentDto
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; } = SalaryComponentType.Earning;

    public ComponentRecurrence Recurrence { get; set; } = ComponentRecurrence.Monthly;

    public bool IsEpfLiable { get; set; }
    public bool IsApitLiable { get; set; } = true;
    public bool IncludeInOtRate { get; set; }
    public bool IncludeInGrossPay { get; set; } = true;
    public bool BasedOnWorkingDays { get; set; }
    public bool IncludeInNoPay { get; set; } = true;
    public bool IncludeInAllowanceOnlyNoPay { get; set; }

    public ComponentCalculationType CalculationType { get; set; } = ComponentCalculationType.FixedAmount;
    [Range(0, 99999999)] public decimal DefaultValue { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Statutory rates ───────────────────────────────────────────────────────────

public class EpfEtfRateDto
{
    public int Id { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public decimal EmployeeEpfPercent { get; set; }
    public decimal EmployerEpfPercent { get; set; }
    public decimal EmployerEtfPercent { get; set; }
    public string? Notes { get; set; }

    /// <summary>True for the row in force today — the one payroll will actually use.</summary>
    public bool IsCurrent { get; set; }
}

public class SaveEpfEtfRateDto
{
    public int Id { get; set; }
    [Required] public DateTime EffectiveFrom { get; set; }
    [Range(0, 100)] public decimal EmployeeEpfPercent { get; set; } = 8m;
    [Range(0, 100)] public decimal EmployerEpfPercent { get; set; } = 12m;
    [Range(0, 100)] public decimal EmployerEtfPercent { get; set; } = 3m;
    [MaxLength(300)] public string? Notes { get; set; }
}

public class ApitBracketDto
{
    public int Id { get; set; }

    public int ApitTaxTableId { get; set; }
    public string TaxTableName { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public decimal FromAmount { get; set; }
    public decimal? ToAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal Relief { get; set; }
    public int SortOrder { get; set; }

    public string RangeDisplay =>
        ToAmount.HasValue ? $"{FromAmount:N0} – {ToAmount:N0}" : $"Over {FromAmount:N0}";
}

public class SaveApitBracketDto
{
    public int Id { get; set; }

    /// <summary>Which table this band belongs to. Bands are meaningless without one.</summary>
    [Required] public int ApitTaxTableId { get; set; }
    [Required] public DateTime EffectiveFrom { get; set; }
    [Range(0, 99999999)] public decimal FromAmount { get; set; }
    public decimal? ToAmount { get; set; }
    [Range(0, 100)] public decimal Rate { get; set; }
    [Range(0, 99999999)] public decimal Relief { get; set; }
    public int SortOrder { get; set; }
}

// ── Employee payroll details ──────────────────────────────────────────────────

/// <summary>
/// One employee's payroll record, with the lookups resolved for display.
///
/// <see cref="BasicSalary"/> is copied from the grade rather than stored here — the grade is
/// the single source, and showing it means nobody has to open a second screen to see what
/// assigning a grade actually pays.
/// </summary>
public class EmployeePayrollInfoDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;

    public string? EpfNumber { get; set; }
    public string? EtfNumber { get; set; }
    public bool IsEpfMember { get; set; } = true;
    public bool IsEtfMember { get; set; } = true;
    public bool IsApitApplicable { get; set; } = true;

    public int? ApitTaxTableId { get; set; }
    public string? ApitTaxTableName { get; set; }
    public bool IsTaxOnTax { get; set; }
    public decimal AdditionalTaxAmount { get; set; }

    public int? EmploymentCategoryId { get; set; }
    public string? EmploymentCategoryName { get; set; }
    public decimal OtLimitHours { get; set; }

    public int? EpfRegistrationBranchId { get; set; }
    public string? EpfRegistrationBranchName { get; set; }
    public string? EpfStatus { get; set; }

    /// <summary>Null means the company rate in force applies.</summary>
    public decimal? EmployeeEpfPercentOverride { get; set; }
    public decimal? EmployerEpfPercentOverride { get; set; }
    public decimal? EmployerEtfPercentOverride { get; set; }

    public int? SalaryGradeId { get; set; }
    public string? SalaryGradeName { get; set; }

    /// <summary>What the grade pays, before any personal override.</summary>
    public decimal GradeBasicSalary { get; set; }

    /// <summary>This employee's own basic, when it differs from the grade. Null = use the grade.</summary>
    public decimal? BasicSalaryOverride { get; set; }

    /// <summary>What will actually be paid — the override when set, otherwise the grade.</summary>
    public decimal BasicSalary { get; set; }

    /// <summary>True when the figure came from the override rather than the grade.</summary>
    public bool IsSalaryOverridden { get; set; }

    public int? SalaryGroupId { get; set; }
    public string? SalaryGroupName { get; set; }

    public int? SubDepartmentId { get; set; }
    public string? SubDepartmentName { get; set; }

    public int? BankBranchId { get; set; }
    public string? BankName { get; set; }
    public string? BankBranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public bool IsBankTransfer { get; set; } = true;

    /// <summary>True when no payroll record has been created for this employee yet.</summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// What still has to be filled in before this employee can be paid. Listed rather than
    /// discovered during a payroll run, when it is far more disruptive.
    /// </summary>
    public List<string> MissingForPayroll { get; set; } = new();
}

public class SaveEmployeePayrollInfoDto
{
    [Required] public int EmployeeId { get; set; }

    [MaxLength(30)] public string? EpfNumber { get; set; }
    [MaxLength(30)] public string? EtfNumber { get; set; }
    public bool IsEpfMember { get; set; } = true;
    public bool IsEtfMember { get; set; } = true;
    public bool IsApitApplicable { get; set; } = true;

    public int? ApitTaxTableId { get; set; }
    public bool IsTaxOnTax { get; set; }
    public decimal AdditionalTaxAmount { get; set; }

    public int? EmploymentCategoryId { get; set; }
    public decimal OtLimitHours { get; set; }

    public int? EpfRegistrationBranchId { get; set; }
    [MaxLength(10)] public string? EpfStatus { get; set; }

    /// <summary>Null keeps the company rate. Only set where this person differs.</summary>
    [Range(0, 100)] public decimal? EmployeeEpfPercentOverride { get; set; }
    [Range(0, 100)] public decimal? EmployerEpfPercentOverride { get; set; }
    [Range(0, 100)] public decimal? EmployerEtfPercentOverride { get; set; }

    public int? SalaryGradeId { get; set; }

    /// <summary>Null clears the override and returns the employee to the grade.</summary>
    [Range(0, 99999999)] public decimal? BasicSalaryOverride { get; set; }

    public int? SalaryGroupId { get; set; }
    public int? SubDepartmentId { get; set; }

    public int? BankBranchId { get; set; }
    [MaxLength(30)] public string? AccountNumber { get; set; }
    [MaxLength(150)] public string? AccountName { get; set; }
    public bool IsBankTransfer { get; set; } = true;
}

/// <summary>An employee's allowance or deduction, showing whether it is the default or an override.</summary>
public class EmployeeComponentDto
{
    public int SalaryComponentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; }
    public string ComponentTypeDisplay => ComponentType == SalaryComponentType.Earning ? "Earning" : "Deduction";

    public ComponentRecurrence Recurrence { get; set; }
    public bool IsEpfLiable { get; set; }

    /// <summary>The component's default, for comparison.</summary>
    public decimal DefaultValue { get; set; }

    /// <summary>What this employee actually gets — the override when set, otherwise the default.</summary>
    public decimal EffectiveValue { get; set; }

    /// <summary>True when an employee-specific value overrides the default.</summary>
    public bool HasOverride { get; set; }

    public int? OverrideId { get; set; }
}

public class SaveEmployeeComponentDto
{
    [Required] public int EmployeeId { get; set; }
    [Required] public int SalaryComponentId { get; set; }

    /// <summary>Null clears the override and returns the employee to the component default.</summary>
    public decimal? Value { get; set; }
}

// ── Employment categories & tax tables ────────────────────────────────────────

public class EmploymentCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>Whether staff in this category join EPF and ETF by default.</summary>
    public bool IsEpfEligible { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int EmployeeCount { get; set; }
}

public class SaveEmploymentCategoryDto
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    public bool IsEpfEligible { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class ApitTaxTableDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public TaxTableType TableType { get; set; }
    public string TableTypeDisplay { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public int BandCount { get; set; }
}

public class SaveApitTaxTableDto
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public TaxTableType TableType { get; set; } = TaxTableType.Monthly;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One row of the payroll employee list — who is set up, and who is not.
///
/// Carries the readiness flag so the list itself answers "who cannot be paid yet", rather
/// than that only becoming visible one employee at a time.
/// </summary>
public class EmployeePayrollListItemDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;

    public string? GradeName { get; set; }

    /// <summary>What is actually paid — the override when set, otherwise the grade.</summary>
    public decimal BasicSalary { get; set; }

    /// <summary>True when this employee has a salary of their own rather than the grade's.</summary>
    public bool IsSalaryOverridden { get; set; }
    public string? CategoryName { get; set; }
    public string? EpfNumber { get; set; }
    public bool IsEpfMember { get; set; }
    public string? BankAccount { get; set; }

    /// <summary>Nothing outstanding — this employee can be included in a run.</summary>
    public bool IsReady { get; set; }

    /// <summary>What is outstanding, for a tooltip rather than a second screen.</summary>
    public List<string> Missing { get; set; } = new();
}

/// <summary>
/// A direct salary entry — the fast path for setting one employee's basic.
///
/// Deliberately narrow: this screen exists to type a number against a person, so it carries
/// only that. Everything else about their payroll is edited where it belongs.
/// </summary>
public class SaveEmployeeSalaryDto
{
    [Required] public int EmployeeId { get; set; }

    /// <summary>Null clears the override and returns the employee to their grade's basic.</summary>
    [Range(0, 99999999)] public decimal? Salary { get; set; }
}

// ── Loan types ────────────────────────────────────────────────────────────────

public class LoanTypeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LoanInterestType InterestType { get; set; }
    public string InterestTypeDisplay =>
        InterestType == LoanInterestType.Fixed ? "Fixed (flat)" : "Reducing balance";
    public decimal InterestRate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Reads as "Interest free" rather than "0%", which looks like a missing value.</summary>
    public string RateDisplay => InterestRate == 0 ? "Interest free" : $"{InterestRate:0.##}%";
}

public class SaveLoanTypeDto
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string Description { get; set; } = string.Empty;
    public LoanInterestType InterestType { get; set; } = LoanInterestType.Fixed;
    [Range(0, 100)] public decimal InterestRate { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Third-party deductions ────────────────────────────────────────────────────

public class ThirdPartyDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Address { get; set; }

    public int? SalaryComponentId { get; set; }
    public string? DeductionCode { get; set; }
    public string? DeductionName { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True when no deduction feeds this payee — recorded but not yet collecting anything.
    /// Shown so a payee that will never receive a remittance is visible.
    /// </summary>
    public bool HasNoDeduction => SalaryComponentId == null;
}

public class SaveThirdPartyDto
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string CompanyName { get; set; } = string.Empty;
    [MaxLength(500)] public string? Address { get; set; }
    public int? SalaryComponentId { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Branch payroll parameters ─────────────────────────────────────────────────

public class BranchPayrollSettingsDto
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    /// <summary>Lives on Branch itself — shown here because this is where it is maintained.</summary>
    public string? EpfEmployerNumber { get; set; }
    public string? EtfEmployerNumber { get; set; }

    public string? EpfDCode { get; set; }
    public string? EpfContactPerson { get; set; }
    public string? EpfContactPhone { get; set; }
    public string? PayeRegistrationNo { get; set; }
    public int NonCitizenTaxYears { get; set; } = 4;

    public decimal? EmployeeEpfPercent { get; set; }
    public decimal? EmployerEpfPercent { get; set; }
    public decimal? EmployerEtfPercent { get; set; }

    public int DaysPerMonth { get; set; } = 30;
    public decimal HoursPerDay { get; set; } = 8m;

    public int? BankBranchId { get; set; }
    public string? BankName { get; set; }
    public string? BankBranchName { get; set; }
    public string? AccountNumber { get; set; }

    public decimal GratuityPercentOfBasic { get; set; } = 50m;
    public int GratuityQualifyingYears { get; set; } = 5;

    public bool RoundOffSalaryPayable { get; set; }
    public decimal RoundNearest { get; set; } = 1m;
    public bool CarryForwardMinusSalary { get; set; } = true;
    public bool CarryForwardCoins { get; set; } = true;

    public RoundingMode EpfRounding { get; set; }
    public RoundingMode EtfRounding { get; set; }
    public RoundingMode NoPayRounding { get; set; }
    public RoundingMode TaxRounding { get; set; }
    public RoundingMode LoanRounding { get; set; }
    public RoundingMode OvertimeRounding { get; set; }

    /// <summary>True when this branch has no parameters saved yet and is showing defaults.</summary>
    public bool IsNew { get; set; }

    /// <summary>The company rates a blank percentage falls back to, for the screen to name.</summary>
    public decimal CompanyEmployeeEpfPercent { get; set; }
    public decimal CompanyEmployerEpfPercent { get; set; }
    public decimal CompanyEmployerEtfPercent { get; set; }
}

public class SaveBranchPayrollSettingsDto
{
    [Required] public int BranchId { get; set; }

    [MaxLength(50)] public string? EpfEmployerNumber { get; set; }
    [MaxLength(50)] public string? EtfEmployerNumber { get; set; }
    [MaxLength(10)] public string? EpfDCode { get; set; }
    [MaxLength(150)] public string? EpfContactPerson { get; set; }
    [MaxLength(30)] public string? EpfContactPhone { get; set; }
    [MaxLength(50)] public string? PayeRegistrationNo { get; set; }
    [Range(0, 50)] public int NonCitizenTaxYears { get; set; } = 4;

    /// <summary>Null keeps the company rate.</summary>
    [Range(0, 100)] public decimal? EmployeeEpfPercent { get; set; }
    [Range(0, 100)] public decimal? EmployerEpfPercent { get; set; }
    [Range(0, 100)] public decimal? EmployerEtfPercent { get; set; }

    [Range(1, 31)] public int DaysPerMonth { get; set; } = 30;
    [Range(1, 24)] public decimal HoursPerDay { get; set; } = 8m;

    public int? BankBranchId { get; set; }
    [MaxLength(30)] public string? AccountNumber { get; set; }

    [Range(0, 100)] public decimal GratuityPercentOfBasic { get; set; } = 50m;
    [Range(0, 50)] public int GratuityQualifyingYears { get; set; } = 5;

    public bool RoundOffSalaryPayable { get; set; }
    [Range(0.01, 1000)] public decimal RoundNearest { get; set; } = 1m;
    public bool CarryForwardMinusSalary { get; set; } = true;
    public bool CarryForwardCoins { get; set; } = true;

    public RoundingMode EpfRounding { get; set; } = RoundingMode.Decimal;
    public RoundingMode EtfRounding { get; set; } = RoundingMode.Decimal;
    public RoundingMode NoPayRounding { get; set; } = RoundingMode.Decimal;
    public RoundingMode TaxRounding { get; set; } = RoundingMode.RoundOff;
    public RoundingMode LoanRounding { get; set; } = RoundingMode.Decimal;
    public RoundingMode OvertimeRounding { get; set; } = RoundingMode.Decimal;
}

// ── Bulk operations ───────────────────────────────────────────────────────────

/// <summary>One employee's bank details, for the bulk maintenance grid.</summary>
public class EmployeeBankRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    public int? BankBranchId { get; set; }
    public string? BankName { get; set; }
    public string? BankBranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public bool IsBankTransfer { get; set; } = true;

    /// <summary>Paid by transfer but missing a branch or account — cannot be included in the file.</summary>
    public bool IsIncomplete =>
        IsBankTransfer && (BankBranchId == null || string.IsNullOrWhiteSpace(AccountNumber));
}

/// <summary>One row of the bank grid on its way back. Only the fields the grid edits.</summary>
public class SaveEmployeeBankRowDto
{
    [Required] public int EmployeeId { get; set; }
    public int? BankBranchId { get; set; }
    [MaxLength(30)] public string? AccountNumber { get; set; }
    [MaxLength(150)] public string? AccountName { get; set; }
    public bool IsBankTransfer { get; set; } = true;
}

/// <summary>
/// Assigns one allowance or deduction to many employees at once.
///
/// <see cref="Value"/> null clears the override for everyone selected, returning them to the
/// component default — the same meaning a blank box has on the individual screen.
/// </summary>
public class BulkAssignComponentDto
{
    [Required] public int SalaryComponentId { get; set; }
    [Required, MinLength(1)] public List<int> EmployeeIds { get; set; } = new();
    public decimal? Value { get; set; }
}

public class BulkAssignResultDto
{
    public int Applied { get; set; }
    public int Cleared { get; set; }
    public string ComponentName { get; set; } = string.Empty;

    public string Summary => Cleared > 0
        ? $"{Cleared} employee(s) returned to the default for {ComponentName}."
        : $"{ComponentName} set for {Applied} employee(s).";
}

// ── EPF adjustments ───────────────────────────────────────────────────────────

public class EpfAdjustmentDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodDisplay => new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public EpfAdjustmentTarget Target { get; set; }
    public string TargetDisplay => Target switch
    {
        EpfAdjustmentTarget.EmployeeEpf => "EPF — employee",
        EpfAdjustmentTarget.EmployerEpf => "EPF — employer",
        _ => "ETF — employer"
    };

    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool AffectsReturn { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
}

public class SaveEpfAdjustmentDto
{
    public int Id { get; set; }
    [Required] public int EmployeeId { get; set; }
    [Range(1, 12)] public int Month { get; set; }
    [Range(2000, 2100)] public int Year { get; set; }
    public EpfAdjustmentTarget Target { get; set; } = EpfAdjustmentTarget.EmployeeEpf;

    /// <summary>Signed — negative recovers an over-contribution.</summary>
    public decimal Amount { get; set; }

    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
    public bool AffectsReturn { get; set; } = true;
}

// ── Payroll suspension ────────────────────────────────────────────────────────

public class NonEffectiveEmployeeDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    /// <summary>"Suspended" for a temporary exclusion, or the employee's own status.</summary>
    public string Reason { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>Only a suspension can be lifted here — a resignation is undone by rejoining.</summary>
    public bool CanRestore { get; set; }
}

public class SuspendEmployeeDto
{
    [Required] public int EmployeeId { get; set; }

    /// <summary>False lifts the suspension and returns the employee to payroll.</summary>
    public bool Suspend { get; set; } = true;

    public DateTime? SuspendedFrom { get; set; }
    public DateTime? SuspendedTo { get; set; }
    [MaxLength(300)] public string? Reason { get; set; }
}

// ── Employee code change ──────────────────────────────────────────────────────

public class ChangeEmployeeCodeDto
{
    [Required] public int EmployeeId { get; set; }
    [Required, MaxLength(50)] public string NewCode { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Sets one component to the same amount across a whole scope, rather than a hand-picked list.
///
/// The companion to <see cref="BulkAssignComponentDto"/>: that one takes employees chosen on
/// screen, this one takes a rule. Both exist because "give these fifteen people a bonus" and
/// "raise the meal allowance for everyone" are different jobs.
/// </summary>
public class CommonValueEntryDto
{
    [Required] public int SalaryComponentId { get; set; }
    public decimal Amount { get; set; }
    public CommonValueScope Scope { get; set; } = CommonValueScope.EmployeesWithItem;
}

public class CommonValueResultDto
{
    public int Affected { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string ScopeDisplay { get; set; } = string.Empty;

    public string Summary =>
        Affected == 0
            ? $"Nothing to change — no employee matched {ScopeDisplay}."
            : $"{ComponentName} set to the new amount for {Affected} employee(s)"
              + (Created > 0 && Updated > 0 ? $" ({Created} new, {Updated} revised)." : ".");
}

// ── Transaction schedule ──────────────────────────────────────────────────────

/// <summary>
/// One scheduled allowance or deduction for an employee, bounded by month.
///
/// The same rows the profile tab edits, with their effective dates exposed. That screen sets
/// an open-ended value; this one says "from August to October" — a temporary allowance that
/// stops on its own rather than needing somebody to remember to remove it.
/// </summary>
public class TransactionScheduleRowDto
{
    public int Id { get; set; }
    public int SalaryComponentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>Month it starts, as yyyymm.</summary>
    public int FromYearMonth { get; set; }

    /// <summary>Month it ends, as yyyymm. Null runs indefinitely.</summary>
    public int? ToYearMonth { get; set; }

    /// <summary>True when this month falls inside the range — what payroll would use now.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Reads as "ended" or "not started yet", so a row that pays nothing says why.</summary>
    public string StatusDisplay { get; set; } = string.Empty;
}

public class SaveTransactionScheduleRowDto
{
    public int Id { get; set; }
    [Required] public int EmployeeId { get; set; }
    [Required] public int SalaryComponentId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>yyyymm — 202608 for August 2026.</summary>
    [Range(200001, 210012)] public int FromYearMonth { get; set; }

    /// <summary>yyyymm, or null to run indefinitely.</summary>
    public int? ToYearMonth { get; set; }
}

// ── Item-wise transaction (one code, one month, many employees) ────────────────

/// <summary>One employee's line on the item-wise grid.</summary>
public class ItemWiseRowDto
{
    /// <summary>Zero when this employee has no figure for the month yet.</summary>
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string? Remarks { get; set; }

    /// <summary>
    /// What this employee's standing value for the same code is, if any. Shown beside the
    /// entry box because a one-off is nearly always judged against the usual figure, and
    /// without it "3500" means nothing on its own.
    /// </summary>
    public decimal? StandingValue { get; set; }
}

public class ItemWiseGridDto
{
    public int SalaryComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentTypeDisplay { get; set; } = string.Empty;
    public int YearMonth { get; set; }

    /// <summary>Set when the month is closed; the grid then loads read-only and says why.</summary>
    public string? LockedReason { get; set; }

    public decimal Total { get; set; }
    public int EnteredCount { get; set; }

    public List<ItemWiseRowDto> Rows { get; set; } = new();
}

public class SaveItemWiseRowDto
{
    public int EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}

public class SaveItemWiseDto
{
    [Required] public int SalaryComponentId { get; set; }

    /// <summary>yyyymm — 202608 for August 2026.</summary>
    [Range(200001, 210012)] public int YearMonth { get; set; }

    public List<SaveItemWiseRowDto> Rows { get; set; } = new();
}

// ── Employee-wise transaction (one employee, one month, many codes) ────────────

/// <summary>One code's line on the employee-wise grid.</summary>
public class EmployeeWiseRowDto
{
    public int Id { get; set; }
    public int SalaryComponentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentTypeDisplay { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public decimal? Hours { get; set; }
    public string? Remarks { get; set; }
}

public class EmployeeWiseGridDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int YearMonth { get; set; }

    public string? LockedReason { get; set; }

    /// <summary>Earnings and deductions kept apart — a single net figure hides which is which.</summary>
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }

    public List<EmployeeWiseRowDto> Rows { get; set; } = new();
}

public class SaveEmployeeWiseRowDto
{
    public int SalaryComponentId { get; set; }
    public decimal Amount { get; set; }
    public decimal? Hours { get; set; }
    public string? Remarks { get; set; }
}

public class SaveEmployeeWiseDto
{
    [Required] public int EmployeeId { get; set; }

    /// <summary>yyyymm — 202608 for August 2026.</summary>
    [Range(200001, 210012)] public int YearMonth { get; set; }

    public List<SaveEmployeeWiseRowDto> Rows { get; set; } = new();
}

// ── Payroll period (the current working month) ─────────────────────────────────

public class PayrollPeriodDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>yyyymm, so screens that speak that format need no conversion.</summary>
    public int YearMonth { get; set; }

    public string MonthDisplay { get; set; } = string.Empty;
    public PayrollStatus Status { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;

    public DateTime? ProcessedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
}

public class OpenPayrollPeriodDto
{
    [Range(2000, 2100)] public int Year { get; set; }
    [Range(1, 12)] public int Month { get; set; }
    public string? Notes { get; set; }
}

public class ReopenPayrollPeriodDto
{
    public int Id { get; set; }
    [Required] public string Reason { get; set; } = string.Empty;
}

// ── Salary increments ─────────────────────────────────────────────────────────

/// <summary>How the employees for an increment were chosen.</summary>
public enum IncrementTarget
{
    Employees = 1,
    Department = 2,
    Grade = 3
}

/// <summary>One employee's before-and-after, shown before anything is written.</summary>
public class IncrementPreviewRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;

    public decimal CurrentBasic { get; set; }
    public decimal IncrementAmount { get; set; }
    public decimal NewBasic { get; set; }

    /// <summary>True when the current basic comes from the grade rather than a personal figure.</summary>
    public bool FromGrade { get; set; }

    /// <summary>Set when this row cannot be incremented, and why. Excluded from the totals.</summary>
    public string? Blocked { get; set; }
}

public class IncrementPreviewDto
{
    public List<IncrementPreviewRowDto> Rows { get; set; } = new();

    public int EligibleCount { get; set; }
    public int BlockedCount { get; set; }
    public decimal TotalCurrent { get; set; }
    public decimal TotalNew { get; set; }
    public decimal MonthlyCostIncrease { get; set; }
}

public class ApplyIncrementDto
{
    public IncrementTarget Target { get; set; } = IncrementTarget.Employees;

    /// <summary>Used when Target is Employees.</summary>
    public List<int> EmployeeIds { get; set; } = new();

    public int? DepartmentId { get; set; }
    public int? SalaryGradeId { get; set; }

    [Range(0.01, 99999999)] public decimal Value { get; set; }
    public IncrementBasis Basis { get; set; } = IncrementBasis.Amount;

    public DateTime EffectiveDate { get; set; } = DateTime.Today;
    public string? Reason { get; set; }
}

public class SalaryIncrementDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; }
    public decimal PreviousBasic { get; set; }
    public decimal NewBasic { get; set; }
    public decimal IncrementValue { get; set; }
    public IncrementBasis Basis { get; set; }
    public string BasisDisplay { get; set; } = string.Empty;
    public IncrementStatus Status { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public Guid? BatchId { get; set; }
}

/// <summary>One row on the Increment Confirmation grid.</summary>
public class IncrementConfirmationRowDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>What they are paid now — still the current salary while this is pending.</summary>
    public decimal BasicSalary { get; set; }

    public DateTime JoiningDate { get; set; }

    /// <summary>Whole years from joining to the effective date, not to today.</summary>
    public int YearsOfService { get; set; }

    public DateTime EffectiveDate { get; set; }

    /// <summary>Why this raise was proposed — annual review, promotion.</summary>
    public string Condition { get; set; } = string.Empty;

    public decimal IncrementAmount { get; set; }
    public decimal NewBasic { get; set; }
    public string BasisDisplay { get; set; } = string.Empty;

    public Guid? BatchId { get; set; }
    public DateTime ProposedAt { get; set; }
}

public class ConfirmIncrementsDto
{
    public List<int> Ids { get; set; } = new();
}

public class RejectIncrementsDto
{
    public List<int> Ids { get; set; } = new();
    [Required] public string Reason { get; set; } = string.Empty;
}

// ── Payroll run ───────────────────────────────────────────────────────────────

public class PayrollRunResultDto
{
    public int PayrollPeriodId { get; set; }
    public string MonthDisplay { get; set; } = string.Empty;

    public int PayslipCount { get; set; }
    public int Suspended { get; set; }

    /// <summary>How many payslips carry a note worth reading before the money moves.</summary>
    public int WithNotes { get; set; }

    /// <summary>
    /// Who was left out and why, named rather than counted. "237 skipped" tells nobody
    /// which 237 or what to fix.
    /// </summary>
    public List<string> Skipped { get; set; } = new();

    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalCostToCompany { get; set; }
}

// ── Payroll reports ───────────────────────────────────────────────────────────

/// <summary>One employee's row on the pay register.</summary>
public class PayRegisterRowDto
{
    public int PayslipId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;

    public decimal EarnedBasic { get; set; }
    public decimal NoPayDeduction { get; set; }
    public decimal OvertimeAmount { get; set; }
    public decimal SalaryArrears { get; set; }
    public decimal GrossPay { get; set; }

    public decimal EmployeeEpf { get; set; }
    public decimal Apit { get; set; }
    public decimal TotalLoanInstalments { get; set; }
    public decimal TotalOtherDeductions { get; set; }
    public decimal BroughtForward { get; set; }
    public decimal TotalDeductions { get; set; }

    public decimal NetPay { get; set; }
    public decimal CarriedForward { get; set; }

    public decimal EmployerEpf { get; set; }
    public decimal EmployerEtf { get; set; }
    public decimal CostToCompany { get; set; }

    public bool IsBankTransfer { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Each component that reached this payslip, keyed by code. The register's columns are
    /// whatever appears across the month rather than a fixed list, so a code added this
    /// month shows up without anyone changing a report.
    /// </summary>
    public Dictionary<string, decimal> Components { get; set; } = new();
}

public class PayRegisterDto
{
    public int PayrollPeriodId { get; set; }
    public string MonthDisplay { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;

    /// <summary>Earning codes present this month, in payslip order. Register column headings.</summary>
    public List<string> EarningColumns { get; set; } = new();
    public List<string> DeductionColumns { get; set; } = new();

    public List<PayRegisterRowDto> Rows { get; set; } = new();

    public PayRegisterRowDto Totals { get; set; } = new();

    public int BankCount { get; set; }
    public int CashCount { get; set; }
    public decimal BankTotal { get; set; }
    public decimal CashTotal { get; set; }
}

/// <summary>One department's line on the pay summary.</summary>
public class PaySummaryRowDto
{
    public string DepartmentName { get; set; } = string.Empty;
    public int Headcount { get; set; }

    public decimal GrossPay { get; set; }
    public decimal EmployeeEpf { get; set; }
    public decimal Apit { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public decimal EmployerEpf { get; set; }
    public decimal EmployerEtf { get; set; }
    public decimal CostToCompany { get; set; }
}

public class PaySummaryDto
{
    public int PayrollPeriodId { get; set; }
    public string MonthDisplay { get; set; } = string.Empty;
    public List<PaySummaryRowDto> Rows { get; set; } = new();
    public PaySummaryRowDto Totals { get; set; } = new();
}

/// <summary>One employee's payslip, ready to print.</summary>
public class PayslipDto
{
    public int Id { get; set; }
    public string MonthDisplay { get; set; } = string.Empty;

    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string DesignationName { get; set; } = string.Empty;
    public string? EpfNumber { get; set; }
    public DateTime JoiningDate { get; set; }

    public int WorkingDays { get; set; }
    public int PresentDays { get; set; }
    public int LeaveDays { get; set; }
    public decimal NoPayDays { get; set; }

    /// <summary>What the no-pay days cost. Shown beside the day count, not as a deduction.</summary>
    public decimal NoPayDeduction { get; set; }
    public decimal OvertimeHours { get; set; }

    public List<PayslipLineDto> Earnings { get; set; } = new();
    public List<PayslipLineDto> Deductions { get; set; } = new();

    public decimal GrossPay { get; set; }
    public decimal EmployeeEpf { get; set; }
    public decimal Apit { get; set; }
    public decimal BroughtForward { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal CarriedForward { get; set; }

    public decimal EmployerEpf { get; set; }
    public decimal EmployerEtf { get; set; }

    public bool IsBankTransfer { get; set; }
    public string? BankName { get; set; }
    public string? BankBranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Notes { get; set; }
}

public class PayslipLineDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>Outstanding after this instalment, for a loan line. Null otherwise.</summary>
    public decimal? Balance { get; set; }
}

using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs;

public class EmployeeLoanDto
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    public int LoanTypeId { get; set; }
    public string LoanTypeName { get; set; } = string.Empty;

    public DateTime LoanDate { get; set; }
    public decimal InterestRate { get; set; }
    public LoanInterestType InterestType { get; set; }
    public string InterestTypeDisplay =>
        InterestType == LoanInterestType.Fixed ? "Fixed (flat)" : "Reducing balance";

    public decimal LoanAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalPayable { get; set; }

    public int NumberOfInstallments { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public bool ReduceThisMonth { get; set; }

    public int FirstDeductionYear { get; set; }
    public int FirstDeductionMonth { get; set; }
    public string FirstDeductionDisplay =>
        FirstDeductionYear > 0 ? new DateTime(FirstDeductionYear, FirstDeductionMonth, 1).ToString("MMM yyyy") : "—";

    public LoanStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();

    /// <summary>Sum of everything recovered so far — derived from the transactions.</summary>
    public decimal Recovered { get; set; }

    /// <summary>Still owed. Total payable less what has been recovered.</summary>
    public decimal Balance => Math.Round(TotalPayable - Recovered, 2);

    /// <summary>Instalments still to run, at the current monthly figure.</summary>
    public int RemainingInstallments =>
        MonthlyInstallment <= 0 ? 0 : (int)Math.Ceiling(Balance / MonthlyInstallment);

    public List<LoanGuarantorDto> Guarantors { get; set; } = new();
    public string? Notes { get; set; }
    public bool AllowGuarantorsToGrantLoans { get; set; }
}

public class LoanGuarantorDto
{
    public int Position { get; set; }
    public int GuarantorEmployeeId { get; set; }
    public string GuarantorCode { get; set; } = string.Empty;
    public string GuarantorName { get; set; } = string.Empty;

    /// <summary>How many other active loans this person already stands behind.</summary>
    public int OtherActiveGuarantees { get; set; }
}

public class SaveEmployeeLoanDto
{
    public int Id { get; set; }

    [Required] public int EmployeeId { get; set; }
    [Required] public int LoanTypeId { get; set; }
    [Required] public DateTime LoanDate { get; set; }

    /// <summary>Defaults to the loan type's rate, but recorded per loan once granted.</summary>
    [Range(0, 100)] public decimal InterestRate { get; set; }

    [Range(0.01, 99999999)] public decimal LoanAmount { get; set; }
    [Range(1, 600)] public int NumberOfInstallments { get; set; }

    /// <summary>Start deducting in the month of the loan date rather than the next one.</summary>
    public bool ReduceThisMonth { get; set; } = true;

    public bool AllowGuarantorsToGrantLoans { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    /// <summary>Up to four, in order. Empty entries are ignored.</summary>
    public List<int> GuarantorEmployeeIds { get; set; } = new();
}

/// <summary>A preview of the schedule, so the figures are visible before the loan is granted.</summary>
public class LoanScheduleDto
{
    public decimal InterestAmount { get; set; }
    public decimal TotalPayable { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public decimal FinalInstallment { get; set; }

    /// <summary>True when the last instalment differs — worth showing rather than hiding.</summary>
    public bool HasUnevenFinal => FinalInstallment != MonthlyInstallment;
}

/// <summary>
/// Settling a loan early, in part or in full.
///
/// <see cref="NewNumberOfInstallments"/> re-spreads whatever is left after the payment. Null
/// keeps the existing instalment, so a part payment simply shortens the loan.
/// </summary>
public class LoanSettlementDto
{
    [Required] public int EmployeeLoanId { get; set; }
    [Required] public DateTime SettlementDate { get; set; }

    [Range(0.01, 99999999)] public decimal AmountPaying { get; set; }

    public int? NewNumberOfInstallments { get; set; }

    /// <summary>Apply the change from this month rather than next.</summary>
    public bool ReduceThisMonth { get; set; } = true;

    [MaxLength(500)] public string? Notes { get; set; }
}

public class LoanTransactionDto
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public string PeriodDisplay =>
        Year.HasValue && Month.HasValue ? new DateTime(Year.Value, Month.Value, 1).ToString("MMM yyyy") : "—";

    public LoanTransactionType TransactionType { get; set; }
    public string TypeDisplay => TransactionType switch
    {
        LoanTransactionType.Installment => "Instalment",
        LoanTransactionType.Settlement => "Settlement",
        _ => "Adjustment"
    };

    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

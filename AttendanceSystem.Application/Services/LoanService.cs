using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Staff loans: granting them, tracking what has been recovered, and settling them early.
///
/// The balance is always derived from the transactions rather than stored. A running total
/// that is written to drifts from the rows behind it, and a loan balance that cannot be
/// reconciled against its history is one nobody can defend to the borrower.
/// </summary>
public class LoanService : ILoanService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    private readonly IApprovalScopeService _scopes;

    public LoanService(IUnitOfWork uow, IAuditService audit,
                       ICurrentUserContext currentUser, IApprovalScopeService scopes)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
        _scopes = scopes;
    }

    private const string Module = AppConstants.Modules.Payroll;

    /// <summary>Preview only — nothing is stored, so the figures can be seen before granting.</summary>
    public Result<LoanScheduleDto> PreviewSchedule(decimal amount, decimal rate, int months,
                                                   LoanInterestType interestType)
    {
        var s = LoanCalculator.Calculate(amount, rate, months, interestType);
        return Result<LoanScheduleDto>.Success(new LoanScheduleDto
        {
            InterestAmount = s.InterestAmount,
            TotalPayable = s.TotalPayable,
            MonthlyInstallment = s.MonthlyInstallment,
            FinalInstallment = s.FinalInstallment
        });
    }

    public async Task<Result<IEnumerable<EmployeeLoanDto>>> GetLoansAsync(
        int? employeeId, LoanStatus? status)
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.GetAllAsync()).ToDictionary(e => e.Id);
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var types = (await _uow.LoanTypes.GetAllAsync()).ToDictionary(t => t.Id, t => t.Description);

            var loans = (await _uow.EmployeeLoans.FindAsync(
                    l => (employeeId == null || l.EmployeeId == employeeId)
                      && (status == null || l.Status == status)))
                .Where(l => employees.TryGetValue(l.EmployeeId, out var e)
                         && scope.Allows(e.Id, e.DepartmentId))
                .ToList();

            var loanIds = loans.Select(l => l.Id).ToHashSet();

            // Fetched once and grouped, rather than a query per loan — a list of two hundred
            // loans would otherwise be two hundred round trips.
            var transactions = (await _uow.LoanTransactions.GetAllAsync())
                .Where(t => loanIds.Contains(t.EmployeeLoanId))
                .GroupBy(t => t.EmployeeLoanId)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            var guarantors = (await _uow.LoanGuarantors.GetAllAsync())
                .Where(g => loanIds.Contains(g.EmployeeLoanId))
                .ToList();

            // How exposed each guarantor already is, across every active loan.
            var activeLoanIds = (await _uow.EmployeeLoans.FindAsync(l => l.Status == LoanStatus.Active))
                .Select(l => l.Id).ToHashSet();
            var exposure = (await _uow.LoanGuarantors.GetAllAsync())
                .Where(g => activeLoanIds.Contains(g.EmployeeLoanId))
                .GroupBy(g => g.GuarantorEmployeeId)
                .ToDictionary(g => g.Key, g => g.Count());

            var rows = loans
                .OrderByDescending(l => l.LoanDate)
                .Select(l =>
                {
                    employees.TryGetValue(l.EmployeeId, out var e);

                    var dto = new EmployeeLoanDto
                    {
                        Id = l.Id, EmployeeId = l.EmployeeId,
                        EmployeeCode = e?.EmployeeCode ?? string.Empty,
                        EmployeeName = e == null ? string.Empty : $"{e.FirstName} {e.LastName}".Trim(),
                        Department = e != null && departments.TryGetValue(e.DepartmentId, out var dn)
                            ? dn : string.Empty,
                        LoanTypeId = l.LoanTypeId,
                        LoanTypeName = types.TryGetValue(l.LoanTypeId, out var tn) ? tn : string.Empty,
                        LoanDate = l.LoanDate,
                        InterestRate = l.InterestRate, InterestType = l.InterestType,
                        LoanAmount = l.LoanAmount, InterestAmount = l.InterestAmount,
                        TotalPayable = l.TotalPayable,
                        NumberOfInstallments = l.NumberOfInstallments,
                        MonthlyInstallment = l.MonthlyInstallment,
                        ReduceThisMonth = l.ReduceThisMonth,
                        FirstDeductionYear = l.FirstDeductionYear,
                        FirstDeductionMonth = l.FirstDeductionMonth,
                        Status = l.Status,
                        AllowGuarantorsToGrantLoans = l.AllowGuarantorsToGrantLoans,
                        Notes = l.Notes,
                        Recovered = transactions.TryGetValue(l.Id, out var rec) ? rec : 0m
                    };

                    dto.Guarantors = guarantors
                        .Where(g => g.EmployeeLoanId == l.Id)
                        .OrderBy(g => g.Position)
                        .Select(g =>
                        {
                            employees.TryGetValue(g.GuarantorEmployeeId, out var ge);
                            return new LoanGuarantorDto
                            {
                                Position = g.Position,
                                GuarantorEmployeeId = g.GuarantorEmployeeId,
                                GuarantorCode = ge?.EmployeeCode ?? string.Empty,
                                GuarantorName = ge == null ? string.Empty
                                    : $"{ge.FirstName} {ge.LastName}".Trim(),
                                // Minus this loan, so the number means "how many others".
                                OtherActiveGuarantees =
                                    Math.Max(0, (exposure.TryGetValue(g.GuarantorEmployeeId, out var n) ? n : 0)
                                                - (l.Status == LoanStatus.Active ? 1 : 0))
                            };
                        }).ToList();

                    return dto;
                });

            return Result<IEnumerable<EmployeeLoanDto>>.Success(rows);
        }
        catch (Exception ex)
        {
            AppLogger.Error("LoanService.GetLoansAsync", ex);
            return Result<IEnumerable<EmployeeLoanDto>>.Failure("Could not load the loans.");
        }
    }

    public async Task<Result> SaveLoanAsync(SaveEmployeeLoanDto dto)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot grant a loan to this employee.");

            var type = await _uow.LoanTypes.GetByIdAsync(dto.LoanTypeId);
            if (type == null) return Result.Failure("That loan type no longer exists.");

            // Somebody cannot stand behind their own loan — the guarantee would be worthless.
            if (dto.GuarantorEmployeeIds.Contains(dto.EmployeeId))
                return Result.Failure("An employee cannot be their own guarantor.");

            var guarantorIds = dto.GuarantorEmployeeIds.Where(id => id > 0).Distinct().Take(4).ToList();

            EmployeeLoan loan;
            var isNew = dto.Id == 0;

            if (isNew)
            {
                loan = new EmployeeLoan { CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now };
                await _uow.EmployeeLoans.AddAsync(loan);
            }
            else
            {
                loan = (await _uow.EmployeeLoans.GetByIdAsync(dto.Id))!;
                if (loan == null) return Result.Failure("Loan not found.");

                // Once money has been recovered the terms are settled between the parties.
                // Changing the amount or the rate then would restate what is owed, so a
                // correction has to be an adjustment transaction instead.
                var recovered = (await _uow.LoanTransactions.FindAsync(t => t.EmployeeLoanId == loan.Id))
                    .Sum(t => t.Amount);

                if (recovered > 0)
                    return Result.Failure(
                        $"{recovered:N2} has already been recovered against this loan, so its terms " +
                        "cannot be changed. Use a settlement or an adjustment instead.");

                loan.ModifiedBy = _currentUser.UserId;
                loan.ModifiedAt = DateTime.Now;
                await _uow.EmployeeLoans.UpdateAsync(loan);
            }

            var schedule = LoanCalculator.Calculate(
                dto.LoanAmount, dto.InterestRate, dto.NumberOfInstallments, type.InterestType);

            loan.EmployeeId = dto.EmployeeId;
            loan.LoanTypeId = dto.LoanTypeId;
            loan.LoanDate = dto.LoanDate.Date;

            // Copied, not referenced: the type may change later, and this loan keeps the
            // terms it was granted on.
            loan.InterestRate = dto.InterestRate;
            loan.InterestType = type.InterestType;

            loan.LoanAmount = dto.LoanAmount;
            loan.InterestAmount = schedule.InterestAmount;
            loan.TotalPayable = schedule.TotalPayable;
            loan.NumberOfInstallments = dto.NumberOfInstallments;
            loan.MonthlyInstallment = schedule.MonthlyInstallment;
            loan.ReduceThisMonth = dto.ReduceThisMonth;

            var first = dto.ReduceThisMonth ? dto.LoanDate.Date : dto.LoanDate.Date.AddMonths(1);
            loan.FirstDeductionYear = first.Year;
            loan.FirstDeductionMonth = first.Month;

            loan.AllowGuarantorsToGrantLoans = dto.AllowGuarantorsToGrantLoans;
            loan.Notes = dto.Notes?.Trim();
            loan.Status = LoanStatus.Active;

            await _uow.SaveChangesAsync();

            // Replaced wholesale rather than diffed — four rows, and a partial update would
            // need to reason about positions changing.
            foreach (var existing in await _uow.LoanGuarantors.FindAsync(g => g.EmployeeLoanId == loan.Id))
                await _uow.LoanGuarantors.DeleteAsync(existing.Id);

            var position = 1;
            foreach (var gid in guarantorIds)
            {
                await _uow.LoanGuarantors.AddAsync(new LoanGuarantor
                {
                    EmployeeLoanId = loan.Id,
                    GuarantorEmployeeId = gid,
                    Position = position++,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                });
            }

            await _uow.SaveChangesAsync();

            await _audit.LogAsync(Module, isNew ? "GrantLoan" : "UpdateLoan",
                _currentUser.UserId, nameof(EmployeeLoan), loan.Id,
                newValues: $"{employee.EmployeeCode} {type.Description} {dto.LoanAmount:N2} " +
                           $"over {dto.NumberOfInstallments} instalments");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("LoanService.SaveLoanAsync", ex);
            return Result.Failure("Could not save the loan.");
        }
    }

    public async Task<Result<IEnumerable<LoanTransactionDto>>> GetTransactionsAsync(int loanId)
    {
        try
        {
            var rows = (await _uow.LoanTransactions.FindAsync(t => t.EmployeeLoanId == loanId))
                .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
                .Select(t => new LoanTransactionDto
                {
                    Id = t.Id, TransactionDate = t.TransactionDate,
                    Year = t.Year, Month = t.Month,
                    TransactionType = t.TransactionType, Amount = t.Amount, Notes = t.Notes
                });

            return Result<IEnumerable<LoanTransactionDto>>.Success(rows);
        }
        catch (Exception ex)
        {
            AppLogger.Error("LoanService.GetTransactionsAsync", ex);
            return Result<IEnumerable<LoanTransactionDto>>.Failure("Could not load the transactions.");
        }
    }

    /// <summary>
    /// Records an early settlement — part or full — and re-spreads whatever is left.
    /// </summary>
    public async Task<Result> SettleAsync(LoanSettlementDto dto)
    {
        try
        {
            var loan = await _uow.EmployeeLoans.GetByIdAsync(dto.EmployeeLoanId);
            if (loan == null) return Result.Failure("Loan not found.");

            var employee = await _uow.Employees.GetByIdAsync(loan.EmployeeId);
            var scope = await _scopes.GetDataScopeAsync();
            if (employee == null || !scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot settle this loan.");

            if (loan.Status != LoanStatus.Active)
                return Result.Failure($"This loan is already {loan.Status.ToString().ToLowerInvariant()}.");

            var recovered = (await _uow.LoanTransactions.FindAsync(t => t.EmployeeLoanId == loan.Id))
                .Sum(t => t.Amount);
            var outstanding = Math.Round(loan.TotalPayable - recovered, 2);

            if (dto.AmountPaying > outstanding)
                return Result.Failure(
                    $"Only {outstanding:N2} is outstanding — paying more would leave the loan in credit.");

            await _uow.LoanTransactions.AddAsync(new LoanTransaction
            {
                EmployeeLoanId = loan.Id,
                TransactionDate = dto.SettlementDate.Date,
                Year = dto.SettlementDate.Year,
                Month = dto.SettlementDate.Month,
                TransactionType = LoanTransactionType.Settlement,
                Amount = dto.AmountPaying,
                Notes = dto.Notes?.Trim(),
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.Now
            });

            var remaining = Math.Round(outstanding - dto.AmountPaying, 2);

            if (remaining <= 0)
            {
                loan.Status = LoanStatus.Settled;
                loan.MonthlyInstallment = 0m;
            }
            else if (dto.NewNumberOfInstallments is > 0)
            {
                // Interest is not recalculated — it was fixed when the loan was granted, and
                // re-deriving it here would change the cost of a loan already agreed.
                var s = LoanCalculator.Reschedule(remaining, dto.NewNumberOfInstallments.Value);
                loan.MonthlyInstallment = s.MonthlyInstallment;
                loan.NumberOfInstallments = dto.NewNumberOfInstallments.Value;
            }

            loan.ModifiedBy = _currentUser.UserId;
            loan.ModifiedAt = DateTime.Now;
            await _uow.EmployeeLoans.UpdateAsync(loan);
            await _uow.SaveChangesAsync();

            await _audit.LogAsync(Module, "SettleLoan", _currentUser.UserId,
                nameof(EmployeeLoan), loan.Id,
                newValues: $"Paid {dto.AmountPaying:N2}, balance {remaining:N2}, " +
                           $"status {loan.Status}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("LoanService.SettleAsync", ex);
            return Result.Failure("Could not settle the loan.");
        }
    }
}

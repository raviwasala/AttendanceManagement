using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// One-off allowances and deductions for a payroll month.
///
/// Entered item-wise — pick one code, then key an amount against each employee — because
/// that is how the source document arrives. A travelling incentive comes in as one claim
/// sheet listing everybody who travelled, not as a stack of per-employee forms, and a screen
/// that made you open each employee in turn would be transcribing that sheet the hard way.
///
/// Distinct from <see cref="EmployeePayrollService"/>'s standing components in exactly the
/// way <see cref="MonthlyTransaction"/> describes: these are paid once and do not repeat.
/// </summary>
public class MonthlyTransactionService : IMonthlyTransactionService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    private readonly IApprovalScopeService _scopes;
    private readonly IAttendanceLockService _locks;

    public MonthlyTransactionService(IUnitOfWork uow, IAuditService audit,
                                     ICurrentUserContext currentUser,
                                     IApprovalScopeService scopes,
                                     IAttendanceLockService locks)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
        _scopes = scopes;
        _locks = locks;
    }

    private static DateTime MonthStart(int yyyymm) => new(yyyymm / 100, yyyymm % 100, 1);
    private static DateTime MonthEnd(int yyyymm) => MonthStart(yyyymm).AddMonths(1).AddDays(-1);

    /// <summary>
    /// Why this month cannot be edited, or null.
    ///
    /// Checked against the same period lock attendance uses, deliberately. A closed month
    /// means "this payroll is done and paid"; letting a bonus be added afterwards would make
    /// the payslip and the ledger disagree, and the lock would be worth nothing. The last day
    /// is tested because that is the date the lock is most likely to cover.
    /// </summary>
    private async Task<string?> LockRefusalAsync(int yyyymm)
    {
        var periodLock = await _locks.GetLockForAsync(MonthEnd(yyyymm));
        if (periodLock == null) return null;

        return $"{MonthStart(yyyymm):MMMM yyyy} is closed " +
               $"({periodLock.FromDate:dd-MMM-yyyy} – {periodLock.ToDate:dd-MMM-yyyy}: {periodLock.Reason}). " +
               "Reopen it first if this really needs changing.";
    }

    public async Task<Result<ItemWiseGridDto>> GetItemWiseAsync(
        int salaryComponentId, int yearMonth, int? departmentId, string? search)
    {
        try
        {
            var component = await _uow.SalaryComponents.GetByIdAsync(salaryComponentId);
            if (component == null)
                return Result<ItemWiseGridDto>.Failure("That allowance or deduction no longer exists.");

            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .Where(e => departmentId == null || e.DepartmentId == departmentId)
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                employees = employees.Where(e =>
                    (e.EmployeeCode ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (e.FullName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            var existing = (await _uow.MonthlyTransactions.FindAsync(t =>
                    t.SalaryComponentId == salaryComponentId && t.YearMonth == yearMonth && !t.IsDeleted))
                .ToDictionary(t => t.EmployeeId);

            // The standing value for the same code, so the entry box has something to be
            // judged against. Only rows in force this month count — an allowance that ended
            // in June is not "the usual figure" for August.
            var monthStart = MonthStart(yearMonth);
            var monthEnd = MonthEnd(yearMonth);
            var standing = (await _uow.EmployeeSalaryComponents.FindAsync(c =>
                    c.SalaryComponentId == salaryComponentId && !c.IsDeleted))
                .Where(c => c.EffectiveFrom <= monthEnd && (c.EffectiveTo == null || c.EffectiveTo >= monthStart))
                .GroupBy(c => c.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EffectiveFrom).First().Value);

            var rows = employees
                .OrderBy(e => e.EmployeeCode)
                .Select(e =>
                {
                    existing.TryGetValue(e.Id, out var tx);
                    return new ItemWiseRowDto
                    {
                        Id = tx?.Id ?? 0,
                        EmployeeId = e.Id,
                        EmployeeCode = e.EmployeeCode ?? "",
                        EmployeeName = e.FullName ?? "",
                        DepartmentName = departments.TryGetValue(e.DepartmentId, out var d) ? d : "",
                        Amount = tx?.Amount ?? 0m,
                        Remarks = tx?.Remarks,
                        StandingValue = standing.TryGetValue(e.Id, out var v) ? v : null
                    };
                })
                .ToList();

            return Result<ItemWiseGridDto>.Success(new ItemWiseGridDto
            {
                SalaryComponentId = salaryComponentId,
                ComponentCode = component.Code,
                ComponentName = component.Name,
                ComponentTypeDisplay = component.ComponentType == Domain.Enums.SalaryComponentType.Earning
                    ? "Earning" : "Deduction",
                YearMonth = yearMonth,
                LockedReason = await LockRefusalAsync(yearMonth),
                Rows = rows,
                EnteredCount = rows.Count(r => r.Amount != 0m),
                Total = rows.Sum(r => r.Amount)
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("GetItemWiseAsync failed", ex);
            return Result<ItemWiseGridDto>.Failure("Could not load the transactions.");
        }
    }

    /// <summary>
    /// Saves the whole grid in one go.
    ///
    /// Only the rows the browser sends are touched, so a filtered grid — one department, or a
    /// search — cannot wipe the employees it was not showing. Within those rows a zero means
    /// "no transaction", and the existing row is removed rather than stored as 0.00: a payslip
    /// listing "Travelling Incentive 0.00" is noise, and the payroll run would have to filter
    /// zeros out again everywhere it reads them.
    /// </summary>
    public async Task<Result<string>> SaveItemWiseAsync(SaveItemWiseDto dto)
    {
        try
        {
            var locked = await LockRefusalAsync(dto.YearMonth);
            if (locked != null) return Result<string>.Failure(locked);

            var component = await _uow.SalaryComponents.GetByIdAsync(dto.SalaryComponentId);
            if (component == null)
                return Result<string>.Failure("That allowance or deduction no longer exists.");

            if (dto.Rows.Any(r => r.Amount < 0))
                return Result<string>.Failure(
                    "Amounts cannot be negative. A deduction is already subtracted because of its " +
                    "type — entering it as a negative would add it back.");

            // Refuses rows outside what this user may see, rather than saving them quietly.
            var scope = await _scopes.GetDataScopeAsync();
            var employees = (await _uow.Employees.FindAsync(e => !e.IsDeleted))
                .ToDictionary(e => e.Id);

            foreach (var row in dto.Rows)
            {
                if (!employees.TryGetValue(row.EmployeeId, out var emp) ||
                    !scope.Allows(emp.Id, emp.DepartmentId))
                    return Result<string>.Failure("One of those employees is outside your access.");
            }

            var existing = (await _uow.MonthlyTransactions.FindAsync(t =>
                    t.SalaryComponentId == dto.SalaryComponentId &&
                    t.YearMonth == dto.YearMonth && !t.IsDeleted))
                .ToDictionary(t => t.EmployeeId);

            int added = 0, updated = 0, removed = 0;

            foreach (var row in dto.Rows)
            {
                existing.TryGetValue(row.EmployeeId, out var tx);

                if (row.Amount == 0m)
                {
                    if (tx != null)
                    {
                        tx.IsDeleted = true;
                        tx.ModifiedBy = _currentUser.UserId;
                        tx.ModifiedAt = DateTime.Now;
                        await _uow.MonthlyTransactions.UpdateAsync(tx);
                        removed++;
                    }
                    continue;
                }

                if (tx == null)
                {
                    await _uow.MonthlyTransactions.AddAsync(new MonthlyTransaction
                    {
                        EmployeeId = row.EmployeeId,
                        SalaryComponentId = dto.SalaryComponentId,
                        YearMonth = dto.YearMonth,
                        Amount = row.Amount,
                        Remarks = row.Remarks,
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = DateTime.Now
                    });
                    added++;
                }
                else if (tx.Amount != row.Amount || tx.Remarks != row.Remarks)
                {
                    tx.Amount = row.Amount;
                    tx.Remarks = row.Remarks;
                    tx.ModifiedBy = _currentUser.UserId;
                    tx.ModifiedAt = DateTime.Now;
                    await _uow.MonthlyTransactions.UpdateAsync(tx);
                    updated++;
                }
            }

            await _uow.SaveChangesAsync();

            var summary = $"{component.Code} — {component.Name}, "
                        + $"{MonthStart(dto.YearMonth):MMMM yyyy}: "
                        + $"{added} added, {updated} changed, {removed} removed.";

            await _audit.LogAsync("Payroll", "ItemWiseTransaction", _currentUser.UserId,
                "MonthlyTransaction", dto.SalaryComponentId, null, summary);

            return Result<string>.Success(summary);
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveItemWiseAsync failed", ex);
            return Result<string>.Failure("Could not save the transactions.");
        }
    }

    /// <summary>
    /// Everything entered for one month, across all codes — what the payroll run consumes,
    /// and what lets somebody check a month's one-offs without opening each code in turn.
    /// </summary>
    public async Task<Result<IEnumerable<ItemWiseGridDto>>> GetMonthSummaryAsync(int yearMonth)
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .Select(e => e.Id)
                .ToHashSet();

            var txs = (await _uow.MonthlyTransactions.FindAsync(t => t.YearMonth == yearMonth && !t.IsDeleted))
                .Where(t => employees.Contains(t.EmployeeId))
                .ToList();

            var components = (await _uow.SalaryComponents.GetAllAsync()).ToDictionary(c => c.Id);

            var summary = txs
                .GroupBy(t => t.SalaryComponentId)
                .Select(g =>
                {
                    components.TryGetValue(g.Key, out var c);
                    return new ItemWiseGridDto
                    {
                        SalaryComponentId = g.Key,
                        ComponentCode = c?.Code ?? "",
                        ComponentName = c?.Name ?? "(deleted)",
                        ComponentTypeDisplay = c?.ComponentType == Domain.Enums.SalaryComponentType.Deduction
                            ? "Deduction" : "Earning",
                        YearMonth = yearMonth,
                        EnteredCount = g.Count(),
                        Total = g.Sum(t => t.Amount)
                    };
                })
                .OrderBy(s => s.ComponentCode)
                .ToList();

            return Result<IEnumerable<ItemWiseGridDto>>.Success(summary);
        }
        catch (Exception ex)
        {
            AppLogger.Error("GetMonthSummaryAsync failed", ex);
            return Result<IEnumerable<ItemWiseGridDto>>.Failure("Could not load the month.");
        }
    }

    // ── Employee-wise: one employee, one month, whatever codes apply ──────────────
    //
    // The same rows as the item-wise grid, read the other way round. Deliberately the same
    // table rather than a parallel one: two stores would drift the first time a figure was
    // corrected on one screen, and nobody would know which was right. Item-wise is for
    // entering a claim sheet; this is for answering "what is on this person's payslip this
    // month, and why".

    public async Task<Result<EmployeeWiseGridDto>> GetEmployeeWiseAsync(int employeeId, int yearMonth)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(employeeId);
            if (employee == null) return Result<EmployeeWiseGridDto>.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result<EmployeeWiseGridDto>.Failure("That employee is outside your access.");

            var components = (await _uow.SalaryComponents.GetAllAsync()).ToDictionary(c => c.Id);
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);

            var txs = (await _uow.MonthlyTransactions.FindAsync(t =>
                    t.EmployeeId == employeeId && t.YearMonth == yearMonth && !t.IsDeleted))
                .ToList();

            var rows = txs
                .Select(t =>
                {
                    components.TryGetValue(t.SalaryComponentId, out var c);
                    return new EmployeeWiseRowDto
                    {
                        Id = t.Id,
                        SalaryComponentId = t.SalaryComponentId,
                        Code = c?.Code ?? "",
                        Description = c?.Name ?? "(deleted item)",
                        ComponentTypeDisplay = c?.ComponentType == Domain.Enums.SalaryComponentType.Deduction
                            ? "Deduction" : "Earning",
                        Amount = t.Amount,
                        Hours = t.Hours,
                        Remarks = t.Remarks
                    };
                })
                .OrderBy(r => r.Code)
                .ToList();

            return Result<EmployeeWiseGridDto>.Success(new EmployeeWiseGridDto
            {
                EmployeeId = employee.Id,
                EmployeeCode = employee.EmployeeCode ?? "",
                EmployeeName = employee.FullName ?? "",
                DepartmentName = departments.TryGetValue(employee.DepartmentId, out var d) ? d : "",
                YearMonth = yearMonth,
                LockedReason = await LockRefusalAsync(yearMonth),
                Rows = rows,
                TotalEarnings = rows.Where(r => r.ComponentTypeDisplay == "Earning").Sum(r => r.Amount),
                TotalDeductions = rows.Where(r => r.ComponentTypeDisplay == "Deduction").Sum(r => r.Amount)
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("GetEmployeeWiseAsync failed", ex);
            return Result<EmployeeWiseGridDto>.Failure("Could not load the transactions.");
        }
    }

    /// <summary>
    /// Replaces this employee's whole month.
    ///
    /// Unlike the item-wise save, this one genuinely owns everything for the employee and
    /// month, so a code that has been dropped from the grid is removed. That is only safe
    /// because the grid is never filtered — it always shows the complete month.
    /// </summary>
    public async Task<Result<string>> SaveEmployeeWiseAsync(SaveEmployeeWiseDto dto)
    {
        try
        {
            var locked = await LockRefusalAsync(dto.YearMonth);
            if (locked != null) return Result<string>.Failure(locked);

            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result<string>.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result<string>.Failure("That employee is outside your access.");

            if (dto.Rows.Any(r => r.Amount < 0))
                return Result<string>.Failure(
                    "Amounts cannot be negative. A deduction is already subtracted because of " +
                    "its type — entering it as a negative would add it back.");

            // The unique index would refuse a duplicate anyway, but as a constraint violation
            // rather than as an explanation of what the person did wrong.
            var dupes = dto.Rows.GroupBy(r => r.SalaryComponentId).Where(g => g.Count() > 1).ToList();
            if (dupes.Any())
                return Result<string>.Failure(
                    "The same code appears more than once. Put the whole month's figure on a " +
                    "single line — two lines for one code would make the payslip depend on " +
                    "which is read first.");

            var components = (await _uow.SalaryComponents.GetAllAsync()).ToDictionary(c => c.Id);
            foreach (var row in dto.Rows)
            {
                if (!components.ContainsKey(row.SalaryComponentId))
                    return Result<string>.Failure("One of those codes no longer exists.");
            }

            var existing = (await _uow.MonthlyTransactions.FindAsync(t =>
                    t.EmployeeId == dto.EmployeeId && t.YearMonth == dto.YearMonth && !t.IsDeleted))
                .ToDictionary(t => t.SalaryComponentId);

            int added = 0, updated = 0, removed = 0;
            var keep = dto.Rows.Where(r => r.Amount != 0m)
                               .Select(r => r.SalaryComponentId).ToHashSet();

            foreach (var row in dto.Rows.Where(r => r.Amount != 0m))
            {
                if (existing.TryGetValue(row.SalaryComponentId, out var tx))
                {
                    if (tx.Amount != row.Amount || tx.Hours != row.Hours || tx.Remarks != row.Remarks)
                    {
                        tx.Amount = row.Amount;
                        tx.Hours = row.Hours;
                        tx.Remarks = row.Remarks;
                        tx.ModifiedBy = _currentUser.UserId;
                        tx.ModifiedAt = DateTime.Now;
                        await _uow.MonthlyTransactions.UpdateAsync(tx);
                        updated++;
                    }
                }
                else
                {
                    await _uow.MonthlyTransactions.AddAsync(new MonthlyTransaction
                    {
                        EmployeeId = dto.EmployeeId,
                        SalaryComponentId = row.SalaryComponentId,
                        YearMonth = dto.YearMonth,
                        Amount = row.Amount,
                        Hours = row.Hours,
                        Remarks = row.Remarks,
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = DateTime.Now
                    });
                    added++;
                }
            }

            foreach (var gone in existing.Values.Where(t => !keep.Contains(t.SalaryComponentId)))
            {
                gone.IsDeleted = true;
                gone.ModifiedBy = _currentUser.UserId;
                gone.ModifiedAt = DateTime.Now;
                await _uow.MonthlyTransactions.UpdateAsync(gone);
                removed++;
            }

            await _uow.SaveChangesAsync();

            var summary = $"{employee.EmployeeCode} — {employee.FullName}, "
                        + $"{MonthStart(dto.YearMonth):MMMM yyyy}: "
                        + $"{added} added, {updated} changed, {removed} removed.";

            await _audit.LogAsync("Payroll", "EmployeeWiseTransaction", _currentUser.UserId,
                "MonthlyTransaction", dto.EmployeeId, null, summary);

            return Result<string>.Success(summary);
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveEmployeeWiseAsync failed", ex);
            return Result<string>.Failure("Could not save the transactions.");
        }
    }
}

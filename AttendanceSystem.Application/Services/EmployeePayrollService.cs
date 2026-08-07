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
/// One employee's payroll record: statutory numbers, grade, bank account, and any
/// component values that differ from the defaults.
///
/// Separate from <see cref="PayrollSetupService"/> and gated on the Payroll module rather
/// than PayrollSetup. Setup is a salary structure; this is what a named individual earns,
/// and the two are rarely visible to the same people.
/// </summary>
public class EmployeePayrollService : IEmployeePayrollService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    private readonly IApprovalScopeService _scopes;

    public EmployeePayrollService(IUnitOfWork uow, IAuditService audit,
                                  ICurrentUserContext currentUser, IApprovalScopeService scopes)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
        _scopes = scopes;
    }

    /// <summary>
    /// Every employee this user may see, with their payroll setup and whether it is complete.
    ///
    /// Built as one pass over the lookups rather than a query per employee: with a few hundred
    /// staff, resolving a grade and a bank per row would be a few hundred round trips to render
    /// one page.
    /// </summary>
    public async Task<Result<IEnumerable<EmployeePayrollListItemDto>>> GetListAsync(
        string? search, int? departmentId, bool? readyOnly)
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .Where(e => departmentId == null || e.DepartmentId == departmentId)
                .ToList();

            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync())
                .ToDictionary(i => i.EmployeeId);
            var grades = (await _uow.SalaryGrades.GetAllAsync()).ToDictionary(g => g.Id);
            var categories = (await _uow.EmploymentCategories.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var branches = (await _uow.Branches.GetAllAsync()).ToDictionary(b => b.Id, b => b.Name);
            var bankBranches = (await _uow.BankBranches.GetAllAsync()).ToDictionary(b => b.Id, b => b.Name);

            var rows = new List<EmployeePayrollListItemDto>();

            foreach (var e in employees)
            {
                infos.TryGetValue(e.Id, out var info);

                var row = new EmployeePayrollListItemDto
                {
                    EmployeeId = e.Id,
                    EmployeeCode = e.EmployeeCode,
                    EmployeeName = $"{e.FirstName} {e.LastName}".Trim(),
                    Department = departments.TryGetValue(e.DepartmentId, out var dn) ? dn : string.Empty,
                    Branch = branches.TryGetValue(e.BranchId, out var bn) ? bn : string.Empty,
                    EpfNumber = info?.EpfNumber,
                    IsEpfMember = info?.IsEpfMember ?? true
                };

                if (info?.SalaryGradeId != null && grades.TryGetValue(info.SalaryGradeId.Value, out var grade))
                {
                    row.GradeName = grade.Name;
                    row.BasicSalary = grade.BasicSalary;
                }

                // Override wins, and is flagged so the list shows where a personal salary
                // has been set rather than presenting it as the grade's figure.
                if (info?.BasicSalaryOverride != null)
                {
                    row.BasicSalary = info.BasicSalaryOverride.Value;
                    row.IsSalaryOverridden = true;
                }

                if (info?.EmploymentCategoryId != null)
                    row.CategoryName = categories.TryGetValue(info.EmploymentCategoryId.Value, out var cn)
                        ? cn : null;

                if (info?.BankBranchId != null && !string.IsNullOrWhiteSpace(info.AccountNumber))
                    row.BankAccount = (bankBranches.TryGetValue(info.BankBranchId.Value, out var bbn)
                        ? bbn + " " : string.Empty) + info.AccountNumber;

                // Same rules as the profile tab, kept in one place below so the list and the
                // detail screen can never disagree about who is ready.
                row.Missing = MissingForPayroll(info);
                row.IsReady = row.Missing.Count == 0;

                rows.Add(row);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                rows = rows.Where(r =>
                    r.EmployeeName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.EmployeeCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (r.EpfNumber ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (readyOnly == true) rows = rows.Where(r => r.IsReady).ToList();
            else if (readyOnly == false) rows = rows.Where(r => !r.IsReady).ToList();

            return Result<IEnumerable<EmployeePayrollListItemDto>>.Success(
                rows.OrderBy(r => r.Department).ThenBy(r => r.EmployeeCode));
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.GetListAsync", ex);
            return Result<IEnumerable<EmployeePayrollListItemDto>>.Failure("Could not load the payroll list.");
        }
    }

    /// <summary>
    /// What stops this employee being paid. One implementation, used by both the list and the
    /// detail screen — two copies would drift and disagree about who is ready.
    /// </summary>
    private static List<string> MissingForPayroll(Domain.Entities.EmployeePayrollInfo? info)
    {
        var missing = new List<string>();

        // Either route to a basic salary will do. Someone given a salary directly on the
        // Salary Details screen is payable even without a grade — requiring both would make
        // that screen useless on its own.
        if (info?.SalaryGradeId == null && info?.BasicSalaryOverride == null)
            missing.Add("No salary grade and no salary set — there is no basic salary to pay.");

        if ((info?.IsEpfMember ?? true) && string.IsNullOrWhiteSpace(info?.EpfNumber))
            missing.Add("EPF member with no EPF number — the return cannot be filed.");

        if ((info?.IsEtfMember ?? true) && string.IsNullOrWhiteSpace(info?.EtfNumber))
            missing.Add("ETF member with no ETF number.");

        if ((info?.IsBankTransfer ?? true) &&
            (info?.BankBranchId == null || string.IsNullOrWhiteSpace(info?.AccountNumber)))
            missing.Add("Paid by transfer but no bank branch or account number.");

        return missing;
    }

    public async Task<Result<EmployeePayrollInfoDto>> GetAsync(int employeeId)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(employeeId);
            if (employee == null) return Result<EmployeePayrollInfoDto>.Failure("Employee not found.");

            // Same visibility rule as everywhere else — a department head reads their own
            // people's pay, not the company's.
            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result<EmployeePayrollInfoDto>.Failure("You cannot view this employee's payroll details.");

            var info = (await _uow.EmployeePayrollInfos.FindAsync(i => i.EmployeeId == employeeId))
                .FirstOrDefault();

            var dto = new EmployeePayrollInfoDto
            {
                EmployeeId = employeeId,
                EmployeeName = $"{employee.FirstName} {employee.LastName}",
                EmployeeCode = employee.EmployeeCode,
                IsNew = info == null
            };

            if (info != null)
            {
                dto.Id = info.Id;
                dto.EpfNumber = info.EpfNumber;
                dto.EtfNumber = info.EtfNumber;
                dto.IsEpfMember = info.IsEpfMember;
                dto.IsEtfMember = info.IsEtfMember;
                dto.IsApitApplicable = info.IsApitApplicable;
                dto.ApitTaxTableId = info.ApitTaxTableId;
                dto.IsTaxOnTax = info.IsTaxOnTax;
                dto.AdditionalTaxAmount = info.AdditionalTaxAmount;
                dto.EmploymentCategoryId = info.EmploymentCategoryId;
                dto.OtLimitHours = info.OtLimitHours;
                dto.EpfRegistrationBranchId = info.EpfRegistrationBranchId;
                dto.EpfStatus = info.EpfStatus;
                dto.EmployeeEpfPercentOverride = info.EmployeeEpfPercentOverride;
                dto.EmployerEpfPercentOverride = info.EmployerEpfPercentOverride;
                dto.EmployerEtfPercentOverride = info.EmployerEtfPercentOverride;

                if (info.ApitTaxTableId.HasValue)
                    dto.ApitTaxTableName = (await _uow.ApitTaxTables.GetByIdAsync(info.ApitTaxTableId.Value))?.Name;

                if (info.EmploymentCategoryId.HasValue)
                    dto.EmploymentCategoryName =
                        (await _uow.EmploymentCategories.GetByIdAsync(info.EmploymentCategoryId.Value))?.Name;

                if (info.EpfRegistrationBranchId.HasValue)
                    dto.EpfRegistrationBranchName =
                        (await _uow.Branches.GetByIdAsync(info.EpfRegistrationBranchId.Value))?.Name;

                dto.SalaryGradeId = info.SalaryGradeId;
                dto.SalaryGroupId = info.SalaryGroupId;
                dto.SubDepartmentId = info.SubDepartmentId;
                dto.BankBranchId = info.BankBranchId;
                dto.AccountNumber = info.AccountNumber;
                dto.AccountName = info.AccountName;
                dto.IsBankTransfer = info.IsBankTransfer;

                if (info.SalaryGradeId.HasValue)
                {
                    var grade = await _uow.SalaryGrades.GetByIdAsync(info.SalaryGradeId.Value);
                    dto.SalaryGradeName = grade?.Name;
                    dto.GradeBasicSalary = grade?.BasicSalary ?? 0;
                }

                // The override wins where both exist — it is the more specific statement.
                // Both are carried so the screen can show what the grade would have paid.
                dto.BasicSalaryOverride = info.BasicSalaryOverride;
                dto.IsSalaryOverridden = info.BasicSalaryOverride.HasValue;
                dto.BasicSalary = info.BasicSalaryOverride ?? dto.GradeBasicSalary;

                if (info.SalaryGroupId.HasValue)
                    dto.SalaryGroupName = (await _uow.SalaryGroups.GetByIdAsync(info.SalaryGroupId.Value))?.Name;

                if (info.SubDepartmentId.HasValue)
                    dto.SubDepartmentName = (await _uow.SubDepartments.GetByIdAsync(info.SubDepartmentId.Value))?.Name;

                if (info.BankBranchId.HasValue)
                {
                    var branch = await _uow.BankBranches.GetByIdAsync(info.BankBranchId.Value);
                    dto.BankBranchName = branch?.Name;
                    if (branch != null)
                        dto.BankName = (await _uow.Banks.GetByIdAsync(branch.BankId))?.Name;
                }
            }

            // Listed here rather than discovered mid-run: a payroll that stops halfway to
            // report a missing bank account has already half-processed the month. Shared with
            // the list screen so the two cannot disagree about who is ready.
            dto.MissingForPayroll = MissingForPayroll(info);

            return Result<EmployeePayrollInfoDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.GetAsync", ex);
            return Result<EmployeePayrollInfoDto>.Failure("Could not load the payroll details.");
        }
    }

    public async Task<Result> SaveAsync(SaveEmployeePayrollInfoDto dto)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot edit this employee's payroll details.");

            var info = (await _uow.EmployeePayrollInfos.FindAsync(i => i.EmployeeId == dto.EmployeeId))
                .FirstOrDefault();

            var isNew = info == null;
            var before = isNew ? null : AuditSnapshot.Snapshot(info);

            info ??= new EmployeePayrollInfo { EmployeeId = dto.EmployeeId };

            info.EpfNumber = dto.EpfNumber?.Trim();
            info.EtfNumber = dto.EtfNumber?.Trim();
            info.IsEpfMember = dto.IsEpfMember;
            info.IsEtfMember = dto.IsEtfMember;
            info.IsApitApplicable = dto.IsApitApplicable;
            info.ApitTaxTableId = dto.ApitTaxTableId;
            info.IsTaxOnTax = dto.IsTaxOnTax;
            info.AdditionalTaxAmount = dto.AdditionalTaxAmount;
            info.EmploymentCategoryId = dto.EmploymentCategoryId;
            info.OtLimitHours = dto.OtLimitHours;
            info.EpfRegistrationBranchId = dto.EpfRegistrationBranchId;
            info.EpfStatus = dto.EpfStatus?.Trim();
            info.EmployeeEpfPercentOverride = dto.EmployeeEpfPercentOverride;
            info.EmployerEpfPercentOverride = dto.EmployerEpfPercentOverride;
            info.EmployerEtfPercentOverride = dto.EmployerEtfPercentOverride;
            info.SalaryGradeId = dto.SalaryGradeId;
            info.BasicSalaryOverride = dto.BasicSalaryOverride;
            info.SalaryGroupId = dto.SalaryGroupId;
            info.SubDepartmentId = dto.SubDepartmentId;
            info.BankBranchId = dto.BankBranchId;
            info.AccountNumber = dto.AccountNumber?.Trim();
            info.AccountName = dto.AccountName?.Trim();
            info.IsBankTransfer = dto.IsBankTransfer;

            if (isNew)
            {
                info.CreatedBy = _currentUser.UserId;
                info.CreatedAt = DateTime.Now;
                await _uow.EmployeePayrollInfos.AddAsync(info);
            }
            else
            {
                info.ModifiedBy = _currentUser.UserId;
                info.ModifiedAt = DateTime.Now;
                await _uow.EmployeePayrollInfos.UpdateAsync(info);
            }

            await _uow.SaveChangesAsync();

            // Audited with before/after because a grade change is a pay change, and the
            // bank account is where the money goes — both are worth being able to trace.
            await _audit.LogAsync(AppConstants.Modules.Payroll,
                isNew ? "CreateEmployeePayroll" : "UpdateEmployeePayroll",
                _currentUser.UserId, nameof(EmployeePayrollInfo), info.Id,
                before, AuditSnapshot.Snapshot(info));

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.SaveAsync", ex);
            return Result.Failure("Could not save the payroll details.");
        }
    }

    /// <summary>
    /// Sets one employee's basic salary directly — the Salary Details screen.
    ///
    /// Writes only the override, leaving grade, bank and statutory details untouched. A
    /// narrow screen that quietly cleared the fields it does not show would be worse than
    /// no screen at all.
    /// </summary>
    public async Task<Result> SaveSalaryAsync(SaveEmployeeSalaryDto dto)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot edit this employee's salary.");

            var info = (await _uow.EmployeePayrollInfos.FindAsync(i => i.EmployeeId == dto.EmployeeId))
                .FirstOrDefault();

            var isNew = info == null;
            var before = isNew ? null : AuditSnapshot.Snapshot(info);

            info ??= new EmployeePayrollInfo
            {
                EmployeeId = dto.EmployeeId,
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.Now
            };

            info.BasicSalaryOverride = dto.Salary;

            if (isNew)
            {
                await _uow.EmployeePayrollInfos.AddAsync(info);
            }
            else
            {
                info.ModifiedBy = _currentUser.UserId;
                info.ModifiedAt = DateTime.Now;
                await _uow.EmployeePayrollInfos.UpdateAsync(info);
            }

            await _uow.SaveChangesAsync();

            // Before and after both recorded: this is a pay change, and "what was it before"
            // is the first question asked when one is queried.
            await _audit.LogAsync(AppConstants.Modules.Payroll, "SetSalary",
                _currentUser.UserId, nameof(EmployeePayrollInfo), info.Id,
                before, AuditSnapshot.Snapshot(info));

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.SaveSalaryAsync", ex);
            return Result.Failure("Could not save the salary.");
        }
    }

    // ── Bulk operations ───────────────────────────────────────────────────────

    /// <summary>
    /// Every visible employee's bank details, for the maintenance grid.
    ///
    /// A grid rather than a profile visit each: collecting account numbers is a data-entry
    /// task done once for hundreds of people, and opening a profile per employee turns half
    /// an hour into an afternoon.
    /// </summary>
    public async Task<Result<IEnumerable<EmployeeBankRowDto>>> GetBankRowsAsync(
        string? search, int? departmentId, bool? incompleteOnly)
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();

            var employees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .Where(e => departmentId == null || e.DepartmentId == departmentId)
                .ToList();

            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync()).ToDictionary(i => i.EmployeeId);
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var branches = (await _uow.BankBranches.GetAllAsync()).ToDictionary(b => b.Id);
            var banks = (await _uow.Banks.GetAllAsync()).ToDictionary(b => b.Id, b => b.Name);

            var rows = employees.Select(e =>
            {
                infos.TryGetValue(e.Id, out var info);

                var row = new EmployeeBankRowDto
                {
                    EmployeeId = e.Id,
                    EmployeeCode = e.EmployeeCode,
                    EmployeeName = $"{e.FirstName} {e.LastName}".Trim(),
                    Department = departments.TryGetValue(e.DepartmentId, out var dn) ? dn : string.Empty,
                    BankBranchId = info?.BankBranchId,
                    AccountNumber = info?.AccountNumber,
                    AccountName = info?.AccountName,
                    IsBankTransfer = info?.IsBankTransfer ?? true
                };

                if (row.BankBranchId != null && branches.TryGetValue(row.BankBranchId.Value, out var bb))
                {
                    row.BankBranchName = bb.Name;
                    row.BankName = banks.TryGetValue(bb.BankId, out var bn) ? bn : null;
                }

                return row;
            }).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                rows = rows.Where(r =>
                    r.EmployeeName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.EmployeeCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (r.AccountNumber ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (incompleteOnly == true) rows = rows.Where(r => r.IsIncomplete).ToList();

            return Result<IEnumerable<EmployeeBankRowDto>>.Success(
                rows.OrderBy(r => r.Department).ThenBy(r => r.EmployeeCode));
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.GetBankRowsAsync", ex);
            return Result<IEnumerable<EmployeeBankRowDto>>.Failure("Could not load the bank details.");
        }
    }

    /// <summary>
    /// Saves one row of the bank grid. Writes only the bank fields, leaving grade and
    /// statutory details alone — a grid that cleared what it does not show would be a trap.
    /// </summary>
    public async Task<Result> SaveBankRowAsync(SaveEmployeeBankRowDto dto)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot edit this employee's bank details.");

            var info = (await _uow.EmployeePayrollInfos.FindAsync(i => i.EmployeeId == dto.EmployeeId))
                .FirstOrDefault();

            var isNew = info == null;
            var before = isNew ? null : AuditSnapshot.Snapshot(info);

            info ??= new EmployeePayrollInfo
            {
                EmployeeId = dto.EmployeeId,
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.Now
            };

            info.BankBranchId = dto.BankBranchId;
            info.AccountNumber = dto.AccountNumber?.Trim();
            info.AccountName = dto.AccountName?.Trim();
            info.IsBankTransfer = dto.IsBankTransfer;

            if (isNew) await _uow.EmployeePayrollInfos.AddAsync(info);
            else
            {
                info.ModifiedBy = _currentUser.UserId;
                info.ModifiedAt = DateTime.Now;
                await _uow.EmployeePayrollInfos.UpdateAsync(info);
            }

            await _uow.SaveChangesAsync();

            // Where somebody's salary is sent is worth a trail of its own.
            await _audit.LogAsync(AppConstants.Modules.Payroll, "UpdateBankDetails",
                _currentUser.UserId, nameof(EmployeePayrollInfo), info.Id,
                before, AuditSnapshot.Snapshot(info));

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.SaveBankRowAsync", ex);
            return Result.Failure("Could not save the bank details.");
        }
    }

    /// <summary>
    /// Sets one component to the same value for many employees.
    ///
    /// Applied one row at a time rather than as a single bulk statement so each write goes
    /// through the same override logic as the individual screen — a second implementation
    /// would eventually disagree with it about effective dates.
    /// </summary>
    public async Task<Result<BulkAssignResultDto>> BulkAssignComponentAsync(BulkAssignComponentDto dto)
    {
        try
        {
            var component = await _uow.SalaryComponents.GetByIdAsync(dto.SalaryComponentId);
            if (component == null) return Result<BulkAssignResultDto>.Failure("That component no longer exists.");

            var scope = await _scopes.GetDataScopeAsync();
            var today = DateTime.Today;

            var result = new BulkAssignResultDto { ComponentName = component.Name };

            foreach (var employeeId in dto.EmployeeIds.Distinct())
            {
                var employee = await _uow.Employees.GetByIdAsync(employeeId);

                // Skipped rather than failed: one employee outside the caller's scope should
                // not abandon an operation covering two hundred who are inside it.
                if (employee == null || !scope.Allows(employee.Id, employee.DepartmentId)) continue;

                var existing = (await _uow.EmployeeSalaryComponents.FindAsync(
                        o => o.EmployeeId == employeeId
                          && o.SalaryComponentId == dto.SalaryComponentId
                          && (o.EffectiveTo == null || o.EffectiveTo >= today)))
                    .OrderByDescending(o => o.EffectiveFrom)
                    .FirstOrDefault();

                if (dto.Value == null)
                {
                    if (existing == null) continue;
                    await _uow.EmployeeSalaryComponents.DeleteAsync(existing.Id);
                    result.Cleared++;
                }
                else if (existing != null)
                {
                    existing.Value = dto.Value.Value;
                    existing.ModifiedBy = _currentUser.UserId;
                    existing.ModifiedAt = DateTime.Now;
                    await _uow.EmployeeSalaryComponents.UpdateAsync(existing);
                    result.Applied++;
                }
                else
                {
                    await _uow.EmployeeSalaryComponents.AddAsync(new EmployeeSalaryComponent
                    {
                        EmployeeId = employeeId,
                        SalaryComponentId = dto.SalaryComponentId,
                        Value = dto.Value.Value,
                        EffectiveFrom = today,
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = DateTime.Now
                    });
                    result.Applied++;
                }
            }

            await _uow.SaveChangesAsync();

            await _audit.LogAsync(AppConstants.Modules.Payroll, "BulkAssignComponent",
                _currentUser.UserId, nameof(SalaryComponent), dto.SalaryComponentId,
                newValues: $"{component.Name}: {result.Summary}");

            return Result<BulkAssignResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.BulkAssignComponentAsync", ex);
            return Result<BulkAssignResultDto>.Failure("Could not apply the component.");
        }
    }

    /// <summary>
    /// Sets one component to the same amount across a scope rather than a chosen list.
    ///
    /// The scope is the whole point, and the three are not interchangeable:
    ///
    ///   • <b>All active employees</b> grants the item to people who never had it. Use for a
    ///     company-wide allowance.
    ///   • <b>Employees with the item</b> revises only those already receiving it, and grants
    ///     it to nobody. Use for a rate change.
    ///
    /// Choosing the first when the second was meant quietly puts an allowance on everyone's
    /// payslip, which is why the screen states what each will do before it runs.
    /// </summary>
    public async Task<Result<CommonValueResultDto>> ApplyCommonValueAsync(CommonValueEntryDto dto)
    {
        try
        {
            var component = await _uow.SalaryComponents.GetByIdAsync(dto.SalaryComponentId);
            if (component == null)
                return Result<CommonValueResultDto>.Failure("That component no longer exists.");

            if (dto.Scope == CommonValueScope.CurrentMonthlyTransaction)
                return Result<CommonValueResultDto>.Failure(
                    "Monthly transactions are not built yet, so there is nothing to update. " +
                    "Use one of the other two scopes.");

            var scope = await _scopes.GetDataScopeAsync();
            var today = DateTime.Today;

            var result = new CommonValueResultDto
            {
                ComponentName = component.Name,
                ScopeDisplay = dto.Scope == CommonValueScope.AllActiveEmployees
                    ? "all active employees"
                    : "employees who already have this item"
            };

            var employees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted))
                .Where(e => scope.Allows(e.Id, e.DepartmentId))
                .ToList();

            var existing = (await _uow.EmployeeSalaryComponents.FindAsync(
                    o => o.SalaryComponentId == dto.SalaryComponentId
                      && (o.EffectiveTo == null || o.EffectiveTo >= today)))
                .GroupBy(o => o.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.EffectiveFrom).First());

            foreach (var e in employees)
            {
                existing.TryGetValue(e.Id, out var current);

                // The distinction that makes the second scope safe: no row means this employee
                // does not have the item, and a revision must not invent one for them.
                if (current == null && dto.Scope == CommonValueScope.EmployeesWithItem) continue;

                if (current != null)
                {
                    current.Value = dto.Amount;
                    current.ModifiedBy = _currentUser.UserId;
                    current.ModifiedAt = DateTime.Now;
                    await _uow.EmployeeSalaryComponents.UpdateAsync(current);
                    result.Updated++;
                }
                else
                {
                    await _uow.EmployeeSalaryComponents.AddAsync(new EmployeeSalaryComponent
                    {
                        EmployeeId = e.Id,
                        SalaryComponentId = dto.SalaryComponentId,
                        Value = dto.Amount,
                        EffectiveFrom = today,
                        CreatedBy = _currentUser.UserId,
                        CreatedAt = DateTime.Now
                    });
                    result.Created++;
                }

                result.Affected++;
            }

            await _uow.SaveChangesAsync();

            await _audit.LogAsync(AppConstants.Modules.Payroll, "CommonValueEntry",
                _currentUser.UserId, nameof(SalaryComponent), dto.SalaryComponentId,
                newValues: $"{component.Name} = {dto.Amount} for {result.ScopeDisplay} " +
                           $"({result.Affected} affected)");

            return Result<CommonValueResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.ApplyCommonValueAsync", ex);
            return Result<CommonValueResultDto>.Failure("Could not apply the value.");
        }
    }

    /// <summary>How many employees a given scope would reach, so the screen can say so first.</summary>
    public async Task<Result<int>> CountForScopeAsync(int componentId, CommonValueScope scope)
    {
        try
        {
            var dataScope = await _scopes.GetDataScopeAsync();
            var today = DateTime.Today;

            var employees = (await _uow.Employees.FindAsync(e => e.IsActive && !e.IsDeleted))
                .Where(e => dataScope.Allows(e.Id, e.DepartmentId))
                .Select(e => e.Id)
                .ToHashSet();

            if (scope == CommonValueScope.AllActiveEmployees)
                return Result<int>.Success(employees.Count);

            var withItem = (await _uow.EmployeeSalaryComponents.FindAsync(
                    o => o.SalaryComponentId == componentId
                      && (o.EffectiveTo == null || o.EffectiveTo >= today)))
                .Select(o => o.EmployeeId)
                .Where(employees.Contains)
                .Distinct()
                .Count();

            return Result<int>.Success(withItem);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.CountForScopeAsync", ex);
            return Result<int>.Failure("Could not count the employees.");
        }
    }

    // ── Transaction schedule ──────────────────────────────────────────────────

    /// <summary>yyyymm to the first of that month.</summary>
    private static DateTime FromYearMonth(int yyyymm) =>
        new(yyyymm / 100, yyyymm % 100, 1);

    /// <summary>
    /// yyyymm to the <i>last</i> day of that month.
    ///
    /// The end is inclusive: "to October" means October is paid. Mapping it to the first
    /// would silently drop the final month of every scheduled allowance.
    /// </summary>
    private static DateTime ToYearMonthEnd(int yyyymm) =>
        new DateTime(yyyymm / 100, yyyymm % 100, 1).AddMonths(1).AddDays(-1);

    private static int ToYyyyMm(DateTime d) => d.Year * 100 + d.Month;

    public async Task<Result<IEnumerable<TransactionScheduleRowDto>>> GetScheduleAsync(int employeeId)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(employeeId);
            if (employee == null)
                return Result<IEnumerable<TransactionScheduleRowDto>>.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result<IEnumerable<TransactionScheduleRowDto>>.Failure(
                    "You cannot view this employee's schedule.");

            var components = (await _uow.SalaryComponents.GetAllAsync()).ToDictionary(c => c.Id);
            var today = DateTime.Today;

            var rows = (await _uow.EmployeeSalaryComponents.FindAsync(o => o.EmployeeId == employeeId))
                .OrderBy(o => o.EffectiveFrom)
                .Select(o =>
                {
                    components.TryGetValue(o.SalaryComponentId, out var c);

                    var started = o.EffectiveFrom.Date <= today;
                    var ended = o.EffectiveTo.HasValue && o.EffectiveTo.Value.Date < today;

                    return new TransactionScheduleRowDto
                    {
                        Id = o.Id,
                        SalaryComponentId = o.SalaryComponentId,
                        Code = c?.Code ?? string.Empty,
                        Description = c?.Name ?? string.Empty,
                        Amount = o.Value,
                        FromYearMonth = ToYyyyMm(o.EffectiveFrom),
                        ToYearMonth = o.EffectiveTo.HasValue ? ToYyyyMm(o.EffectiveTo.Value) : null,
                        IsCurrent = started && !ended,
                        // Named rather than left blank: a row paying nothing should say why,
                        // not look like a mistake.
                        StatusDisplay = !started ? "Not started" : ended ? "Ended" : "Running"
                    };
                });

            return Result<IEnumerable<TransactionScheduleRowDto>>.Success(rows);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.GetScheduleAsync", ex);
            return Result<IEnumerable<TransactionScheduleRowDto>>.Failure("Could not load the schedule.");
        }
    }

    public async Task<Result> SaveScheduleRowAsync(SaveTransactionScheduleRowDto dto)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot edit this employee's schedule.");

            if (dto.ToYearMonth.HasValue && dto.ToYearMonth < dto.FromYearMonth)
                return Result.Failure("The end month cannot be before the start month.");

            var component = await _uow.SalaryComponents.GetByIdAsync(dto.SalaryComponentId);
            if (component == null) return Result.Failure("That component no longer exists.");

            var from = FromYearMonth(dto.FromYearMonth);
            var to = dto.ToYearMonth.HasValue ? ToYearMonthEnd(dto.ToYearMonth.Value) : (DateTime?)null;

            // Two rows for the same component covering the same month would make the amount
            // depend on which was read first, so an overlap is refused rather than resolved.
            var siblings = (await _uow.EmployeeSalaryComponents.FindAsync(
                    o => o.EmployeeId == dto.EmployeeId
                      && o.SalaryComponentId == dto.SalaryComponentId
                      && o.Id != dto.Id))
                .ToList();

            var clash = siblings.FirstOrDefault(o =>
                from <= (o.EffectiveTo ?? DateTime.MaxValue) &&
                (to ?? DateTime.MaxValue) >= o.EffectiveFrom);

            if (clash != null)
                return Result.Failure(
                    $"This overlaps an existing {component.Name} entry running from " +
                    $"{clash.EffectiveFrom:MMM yyyy}" +
                    (clash.EffectiveTo.HasValue ? $" to {clash.EffectiveTo.Value:MMM yyyy}" : " onwards") +
                    ". End that one first, or change the dates.");

            EmployeeSalaryComponent row;
            if (dto.Id == 0)
            {
                row = new EmployeeSalaryComponent
                {
                    EmployeeId = dto.EmployeeId,
                    SalaryComponentId = dto.SalaryComponentId,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                };
                await _uow.EmployeeSalaryComponents.AddAsync(row);
            }
            else
            {
                row = (await _uow.EmployeeSalaryComponents.GetByIdAsync(dto.Id))!;
                if (row == null) return Result.Failure("That schedule entry no longer exists.");
                row.ModifiedBy = _currentUser.UserId;
                row.ModifiedAt = DateTime.Now;
                await _uow.EmployeeSalaryComponents.UpdateAsync(row);
            }

            row.Value = dto.Amount;
            row.EffectiveFrom = from;
            row.EffectiveTo = to;

            await _uow.SaveChangesAsync();

            await _audit.LogAsync(AppConstants.Modules.Payroll,
                dto.Id == 0 ? "CreateScheduleEntry" : "UpdateScheduleEntry",
                _currentUser.UserId, nameof(EmployeeSalaryComponent), row.Id,
                newValues: $"{employee.EmployeeCode} {component.Name} {dto.Amount} " +
                           $"{dto.FromYearMonth}–{dto.ToYearMonth?.ToString() ?? "open"}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.SaveScheduleRowAsync", ex);
            return Result.Failure("Could not save the schedule entry.");
        }
    }

    public async Task<Result> DeleteScheduleRowAsync(int id)
    {
        try
        {
            var row = await _uow.EmployeeSalaryComponents.GetByIdAsync(id);
            if (row == null) return Result.Failure("That schedule entry no longer exists.");

            var employee = await _uow.Employees.GetByIdAsync(row.EmployeeId);
            var scope = await _scopes.GetDataScopeAsync();
            if (employee == null || !scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot edit this employee's schedule.");

            await _uow.EmployeeSalaryComponents.DeleteAsync(id);
            await _uow.SaveChangesAsync();

            await _audit.LogAsync(AppConstants.Modules.Payroll, "DeleteScheduleEntry",
                _currentUser.UserId, nameof(EmployeeSalaryComponent), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.DeleteScheduleRowAsync", ex);
            return Result.Failure("Could not delete the schedule entry.");
        }
    }

    // ── EPF adjustments ───────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<EpfAdjustmentDto>>> GetEpfAdjustmentsAsync(int? year, int? month)
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();
            var employees = (await _uow.Employees.GetAllAsync()).ToDictionary(e => e.Id);

            var rows = (await _uow.EpfAdjustments.FindAsync(
                    a => (year == null || a.Year == year) && (month == null || a.Month == month)))
                .Where(a => employees.TryGetValue(a.EmployeeId, out var e)
                         && scope.Allows(e.Id, e.DepartmentId))
                .OrderByDescending(a => a.Year).ThenByDescending(a => a.Month)
                .Select(a =>
                {
                    employees.TryGetValue(a.EmployeeId, out var e);
                    return new EpfAdjustmentDto
                    {
                        Id = a.Id, EmployeeId = a.EmployeeId,
                        EmployeeCode = e?.EmployeeCode ?? string.Empty,
                        EmployeeName = e == null ? string.Empty : $"{e.FirstName} {e.LastName}".Trim(),
                        Year = a.Year, Month = a.Month, Target = a.Target,
                        Amount = a.Amount, Reason = a.Reason, AffectsReturn = a.AffectsReturn,
                        IsApplied = a.AppliedInPayrollPeriodId != null, AppliedAt = a.AppliedAt
                    };
                });

            return Result<IEnumerable<EpfAdjustmentDto>>.Success(rows);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.GetEpfAdjustmentsAsync", ex);
            return Result<IEnumerable<EpfAdjustmentDto>>.Failure("Could not load the adjustments.");
        }
    }

    public async Task<Result> SaveEpfAdjustmentAsync(SaveEpfAdjustmentDto dto)
    {
        try
        {
            if (dto.Amount == 0) return Result.Failure("An adjustment of zero would do nothing.");

            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot adjust this employee's contributions.");

            EpfAdjustment a;
            if (dto.Id == 0)
            {
                a = new EpfAdjustment { CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now };
                await _uow.EpfAdjustments.AddAsync(a);
            }
            else
            {
                a = (await _uow.EpfAdjustments.GetByIdAsync(dto.Id))!;
                if (a == null) return Result.Failure("Adjustment not found.");

                // Once carried into a run it has changed a payslip and possibly a filed
                // return. Editing it then would restate history without any trail.
                if (a.AppliedInPayrollPeriodId != null)
                    return Result.Failure(
                        "This adjustment has already been applied in a payroll run. " +
                        "Raise a further adjustment instead of changing it.");

                a.ModifiedBy = _currentUser.UserId;
                a.ModifiedAt = DateTime.Now;
                await _uow.EpfAdjustments.UpdateAsync(a);
            }

            a.EmployeeId = dto.EmployeeId;
            a.Year = dto.Year; a.Month = dto.Month;
            a.Target = dto.Target; a.Amount = dto.Amount;
            a.Reason = dto.Reason.Trim(); a.AffectsReturn = dto.AffectsReturn;

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Payroll,
                dto.Id == 0 ? "CreateEpfAdjustment" : "UpdateEpfAdjustment",
                _currentUser.UserId, nameof(EpfAdjustment), a.Id,
                newValues: $"{employee.EmployeeCode} {dto.Year}-{dto.Month:00} {dto.Target} {dto.Amount}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.SaveEpfAdjustmentAsync", ex);
            return Result.Failure("Could not save the adjustment.");
        }
    }

    public async Task<Result> DeleteEpfAdjustmentAsync(int id)
    {
        try
        {
            var a = await _uow.EpfAdjustments.GetByIdAsync(id);
            if (a == null) return Result.Failure("Adjustment not found.");

            if (a.AppliedInPayrollPeriodId != null)
                return Result.Failure("This adjustment has already been applied and cannot be removed.");

            await _uow.EpfAdjustments.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Payroll, "DeleteEpfAdjustment",
                _currentUser.UserId, nameof(EpfAdjustment), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.DeleteEpfAdjustmentAsync", ex);
            return Result.Failure("Could not delete the adjustment.");
        }
    }

    // ── Non-effective employees ───────────────────────────────────────────────

    /// <summary>
    /// Everyone not being paid, whatever the reason — suspended, resigned or inactive.
    ///
    /// One list because the question being asked is "who is missing from the payroll", and
    /// splitting it by cause would make that need two screens and a mental join.
    /// </summary>
    public async Task<Result<IEnumerable<NonEffectiveEmployeeDto>>> GetNonEffectiveAsync()
    {
        try
        {
            var scope = await _scopes.GetDataScopeAsync();
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync()).ToDictionary(i => i.EmployeeId);

            var rows = new List<NonEffectiveEmployeeDto>();

            foreach (var e in (await _uow.Employees.GetAllAsync())
                         .Where(e => scope.Allows(e.Id, e.DepartmentId)))
            {
                infos.TryGetValue(e.Id, out var info);

                var suspended = info?.IsPayrollSuspended == true;
                var inactive = !e.IsActive;

                if (!suspended && !inactive) continue;

                rows.Add(new NonEffectiveEmployeeDto
                {
                    EmployeeId = e.Id,
                    EmployeeCode = e.EmployeeCode,
                    EmployeeName = $"{e.FirstName} {e.LastName}".Trim(),
                    Department = departments.TryGetValue(e.DepartmentId, out var dn) ? dn : string.Empty,

                    // Suspension is reported ahead of inactivity: it is the reversible one,
                    // and the one somebody is likely to be looking for.
                    Category = suspended ? "Suspended" : e.Status.ToString(),
                    Reason = suspended
                        ? (info!.SuspendReason ?? "No reason recorded")
                        : (e.ResignationReason ?? e.Status.ToString()),
                    FromDate = suspended ? info!.SuspendedFrom : e.ResignationDate,
                    ToDate = suspended ? info!.SuspendedTo : null,
                    CanRestore = suspended
                });
            }

            return Result<IEnumerable<NonEffectiveEmployeeDto>>.Success(
                rows.OrderBy(r => r.Category).ThenBy(r => r.EmployeeCode));
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.GetNonEffectiveAsync", ex);
            return Result<IEnumerable<NonEffectiveEmployeeDto>>.Failure("Could not load the list.");
        }
    }

    public async Task<Result> SuspendAsync(SuspendEmployeeDto dto)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot change this employee's payroll status.");

            if (dto.Suspend && string.IsNullOrWhiteSpace(dto.Reason))
                return Result.Failure("Give a reason — a payroll clerk should not have to guess "
                                    + "why somebody stopped being paid.");

            var info = (await _uow.EmployeePayrollInfos.FindAsync(i => i.EmployeeId == dto.EmployeeId))
                .FirstOrDefault();

            var isNew = info == null;
            info ??= new EmployeePayrollInfo
            {
                EmployeeId = dto.EmployeeId,
                CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now
            };

            info.IsPayrollSuspended = dto.Suspend;
            info.SuspendedFrom = dto.Suspend ? (dto.SuspendedFrom ?? DateTime.Today) : null;
            info.SuspendedTo = dto.Suspend ? dto.SuspendedTo : null;
            info.SuspendReason = dto.Suspend ? dto.Reason?.Trim() : null;

            if (isNew) await _uow.EmployeePayrollInfos.AddAsync(info);
            else
            {
                info.ModifiedBy = _currentUser.UserId;
                info.ModifiedAt = DateTime.Now;
                await _uow.EmployeePayrollInfos.UpdateAsync(info);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Payroll,
                dto.Suspend ? "SuspendFromPayroll" : "RestoreToPayroll",
                _currentUser.UserId, nameof(EmployeePayrollInfo), info.Id,
                newValues: $"{employee.EmployeeCode}: {dto.Reason}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.SuspendAsync", ex);
            return Result.Failure("Could not change the payroll status.");
        }
    }

    // ── Employee code change ──────────────────────────────────────────────────

    /// <summary>
    /// Renames an employee's code.
    ///
    /// The code appears on payslips, exports and statutory returns, so this is not a field
    /// edit: the change is recorded in the employee's history with the old value and a
    /// reason, so a payslip carrying the previous code can still be traced to the person.
    /// </summary>
    public async Task<Result> ChangeEmployeeCodeAsync(ChangeEmployeeCodeDto dto)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot change this employee's code.");

            var newCode = dto.NewCode.Trim();
            if (string.Equals(newCode, employee.EmployeeCode, StringComparison.OrdinalIgnoreCase))
                return Result.Failure("That is already this employee's code.");

            // Includes deleted employees deliberately: reusing the code of somebody who left
            // would make their historical payslips ambiguous.
            var taken = (await _uow.Employees.FindAsync(e => e.EmployeeCode == newCode && e.Id != dto.EmployeeId))
                .Any();
            if (taken) return Result.Failure($"Code '{newCode}' is already used by another employee.");

            var oldCode = employee.EmployeeCode;

            employee.EmployeeCode = newCode;
            employee.ModifiedBy = _currentUser.UserId;
            employee.ModifiedAt = DateTime.Now;
            await _uow.Employees.UpdateAsync(employee);

            await _uow.SaveChangesAsync();

            await _audit.LogAsync(AppConstants.Modules.Employees, "ChangeEmployeeCode",
                _currentUser.UserId, nameof(Employee), employee.Id,
                oldValues: oldCode, newValues: $"{newCode} — {dto.Reason.Trim()}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.ChangeEmployeeCodeAsync", ex);
            return Result.Failure("Could not change the employee code.");
        }
    }

    // ── Component values ──────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<EmployeeComponentDto>>> GetComponentsAsync(int employeeId)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(employeeId);
            if (employee == null) return Result<IEnumerable<EmployeeComponentDto>>.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result<IEnumerable<EmployeeComponentDto>>.Failure("You cannot view this employee's pay.");

            var components = (await _uow.SalaryComponents.FindAsync(c => c.IsActive)).ToList();

            var today = DateTime.Today;
            var overrides = (await _uow.EmployeeSalaryComponents.FindAsync(
                    o => o.EmployeeId == employeeId
                      && o.EffectiveFrom <= today
                      && (o.EffectiveTo == null || o.EffectiveTo >= today)))
                .ToList();

            // Basic is needed to turn a percentage component into a money figure.
            decimal basic = 0;
            var info = (await _uow.EmployeePayrollInfos.FindAsync(i => i.EmployeeId == employeeId))
                .FirstOrDefault();
            if (info?.SalaryGradeId != null)
                basic = (await _uow.SalaryGrades.GetByIdAsync(info.SalaryGradeId.Value))?.BasicSalary ?? 0;

            var rows = components
                .OrderBy(c => c.ComponentType).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c =>
                {
                    // The latest override wins when several are in force — a pay revision
                    // adds a row rather than editing the old one.
                    var ov = overrides.Where(o => o.SalaryComponentId == c.Id)
                                      .OrderByDescending(o => o.EffectiveFrom)
                                      .FirstOrDefault();

                    var baseValue = ov?.Value ?? c.DefaultValue;

                    var effective = c.CalculationType == ComponentCalculationType.PercentOfBasic
                        ? Math.Round(basic * baseValue / 100m, 2)
                        : baseValue;

                    return new EmployeeComponentDto
                    {
                        SalaryComponentId = c.Id,
                        Name = c.Name, Code = c.Code, ComponentType = c.ComponentType,
                        Recurrence = c.Recurrence, IsEpfLiable = c.IsEpfLiable,
                        DefaultValue = c.DefaultValue,
                        EffectiveValue = effective,
                        HasOverride = ov != null,
                        OverrideId = ov?.Id
                    };
                });

            return Result<IEnumerable<EmployeeComponentDto>>.Success(rows);
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.GetComponentsAsync", ex);
            return Result<IEnumerable<EmployeeComponentDto>>.Failure("Could not load the components.");
        }
    }

    public async Task<Result> SaveComponentAsync(SaveEmployeeComponentDto dto)
    {
        try
        {
            var employee = await _uow.Employees.GetByIdAsync(dto.EmployeeId);
            if (employee == null) return Result.Failure("Employee not found.");

            var scope = await _scopes.GetDataScopeAsync();
            if (!scope.Allows(employee.Id, employee.DepartmentId))
                return Result.Failure("You cannot edit this employee's pay.");

            var today = DateTime.Today;
            var existing = (await _uow.EmployeeSalaryComponents.FindAsync(
                    o => o.EmployeeId == dto.EmployeeId
                      && o.SalaryComponentId == dto.SalaryComponentId
                      && (o.EffectiveTo == null || o.EffectiveTo >= today)))
                .OrderByDescending(o => o.EffectiveFrom)
                .FirstOrDefault();

            if (dto.Value == null)
            {
                // Clearing an override returns the employee to the component default.
                if (existing == null) return Result.Success();

                await _uow.EmployeeSalaryComponents.DeleteAsync(existing.Id);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync(AppConstants.Modules.Payroll, "ClearEmployeeComponent",
                    _currentUser.UserId, nameof(EmployeeSalaryComponent), existing.Id);
                return Result.Success();
            }

            if (existing != null)
            {
                existing.Value = dto.Value.Value;
                existing.ModifiedBy = _currentUser.UserId;
                existing.ModifiedAt = DateTime.Now;
                await _uow.EmployeeSalaryComponents.UpdateAsync(existing);
            }
            else
            {
                await _uow.EmployeeSalaryComponents.AddAsync(new EmployeeSalaryComponent
                {
                    EmployeeId = dto.EmployeeId,
                    SalaryComponentId = dto.SalaryComponentId,
                    Value = dto.Value.Value,
                    EffectiveFrom = today,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                });
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(AppConstants.Modules.Payroll, "SetEmployeeComponent",
                _currentUser.UserId, nameof(EmployeeSalaryComponent), dto.SalaryComponentId,
                newValues: $"Employee {dto.EmployeeId}: {dto.Value}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeePayrollService.SaveComponentAsync", ex);
            return Result.Failure("Could not save the component value.");
        }
    }
}

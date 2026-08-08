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
/// The payroll master data: banks, grades, groups, sub-departments, components and the
/// statutory rate tables.
///
/// One service rather than six, because these are all small reference lists edited on the
/// same screen and never independently of each other. Splitting them would mean six
/// near-identical files and six registrations to keep in step.
///
/// Deletes are refused while anything is using the row. A grade removed from under an
/// employee would leave them with no basic salary and no indication why — payroll master data
/// is exactly where a silent dangling reference turns into a wrong payslip.
/// </summary>
public class PayrollSetupService : IPayrollSetupService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;

    public PayrollSetupService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
    }

    private const string Module = AppConstants.Modules.PayrollSetup;

    private void Stamp(BaseEntity e, bool isNew)
    {
        if (isNew) { e.CreatedBy = _currentUser.UserId; e.CreatedAt = DateTime.Now; }
        else { e.ModifiedBy = _currentUser.UserId; e.ModifiedAt = DateTime.Now; }
    }

    // ── Banks ─────────────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<BankDto>>> GetBanksAsync()
    {
        try
        {
            var banks = (await _uow.Banks.GetAllAsync()).ToList();
            var branches = (await _uow.BankBranches.GetAllAsync()).ToList();

            return Result<IEnumerable<BankDto>>.Success(banks
                .OrderBy(b => b.Name)
                .Select(b => new BankDto
                {
                    Id = b.Id, Name = b.Name, Code = b.Code, IsActive = b.IsActive,
                    BranchCount = branches.Count(x => x.BankId == b.Id)
                }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetBanksAsync", ex);
            return Result<IEnumerable<BankDto>>.Failure("Could not load banks.");
        }
    }

    public async Task<Result> SaveBankAsync(SaveBankDto dto)
    {
        try
        {
            var code = dto.Code.Trim();

            // The code is what the transfer file matches on, so a duplicate would silently
            // route somebody's salary to the wrong bank.
            var clash = (await _uow.Banks.FindAsync(b => b.Code == code && b.Id != dto.Id)).Any();
            if (clash) return Result.Failure($"Bank code '{code}' is already used.");

            if (dto.Id == 0)
            {
                var bank = new Bank { Name = dto.Name.Trim(), Code = code, IsActive = dto.IsActive };
                Stamp(bank, true);
                await _uow.Banks.AddAsync(bank);
            }
            else
            {
                var bank = await _uow.Banks.GetByIdAsync(dto.Id);
                if (bank == null) return Result.Failure("Bank not found.");
                bank.Name = dto.Name.Trim(); bank.Code = code; bank.IsActive = dto.IsActive;
                Stamp(bank, false);
                await _uow.Banks.UpdateAsync(bank);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateBank" : "UpdateBank",
                _currentUser.UserId, nameof(Bank), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveBankAsync", ex);
            return Result.Failure("Could not save the bank.");
        }
    }

    public async Task<Result> DeleteBankAsync(int id)
    {
        try
        {
            if ((await _uow.BankBranches.FindAsync(b => b.BankId == id)).Any())
                return Result.Failure("This bank has branches. Remove them first.");

            await _uow.Banks.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteBank", _currentUser.UserId, nameof(Bank), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteBankAsync", ex);
            return Result.Failure("Could not delete the bank.");
        }
    }

    // ── Bank branches ─────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<BankBranchDto>>> GetBankBranchesAsync(int? bankId)
    {
        try
        {
            var banks = (await _uow.Banks.GetAllAsync()).ToDictionary(b => b.Id);
            var branches = (await _uow.BankBranches.GetAllAsync())
                .Where(b => bankId == null || b.BankId == bankId)
                .ToList();

            return Result<IEnumerable<BankBranchDto>>.Success(branches
                .OrderBy(b => banks.TryGetValue(b.BankId, out var bk) ? bk.Name : "")
                .ThenBy(b => b.Name)
                .Select(b =>
                {
                    var bankName = banks.TryGetValue(b.BankId, out var bk) ? bk.Name : string.Empty;
                    var bankCode = banks.TryGetValue(b.BankId, out var bk2) ? bk2.Code : string.Empty;
                    return new BankBranchDto
                    {
                        Id = b.Id, BankId = b.BankId, BankName = bankName,
                        Name = b.Name, Code = b.Code, IsActive = b.IsActive,
                        FullCode = $"{bankCode}-{b.Code}"
                    };
                }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetBankBranchesAsync", ex);
            return Result<IEnumerable<BankBranchDto>>.Failure("Could not load bank branches.");
        }
    }

    public async Task<Result> SaveBankBranchAsync(SaveBankBranchDto dto)
    {
        try
        {
            var code = dto.Code.Trim();

            // Per bank, not global: branch code 001 exists at every bank.
            var clash = (await _uow.BankBranches.FindAsync(
                b => b.BankId == dto.BankId && b.Code == code && b.Id != dto.Id)).Any();
            if (clash) return Result.Failure($"Branch code '{code}' already exists for this bank.");

            if (dto.Id == 0)
            {
                var branch = new BankBranch
                {
                    BankId = dto.BankId, Name = dto.Name.Trim(), Code = code, IsActive = dto.IsActive
                };
                Stamp(branch, true);
                await _uow.BankBranches.AddAsync(branch);
            }
            else
            {
                var branch = await _uow.BankBranches.GetByIdAsync(dto.Id);
                if (branch == null) return Result.Failure("Bank branch not found.");
                branch.BankId = dto.BankId; branch.Name = dto.Name.Trim();
                branch.Code = code; branch.IsActive = dto.IsActive;
                Stamp(branch, false);
                await _uow.BankBranches.UpdateAsync(branch);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateBankBranch" : "UpdateBankBranch",
                _currentUser.UserId, nameof(BankBranch), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveBankBranchAsync", ex);
            return Result.Failure("Could not save the bank branch.");
        }
    }

    public async Task<Result> DeleteBankBranchAsync(int id)
    {
        try
        {
            if ((await _uow.EmployeePayrollInfos.FindAsync(e => e.BankBranchId == id)).Any())
                return Result.Failure("Employees are paid into this branch. Reassign them first.");

            await _uow.BankBranches.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteBankBranch", _currentUser.UserId, nameof(BankBranch), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteBankBranchAsync", ex);
            return Result.Failure("Could not delete the bank branch.");
        }
    }

    // ── Salary grades ─────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<SalaryGradeDto>>> GetGradesAsync()
    {
        try
        {
            var grades = (await _uow.SalaryGrades.GetAllAsync()).ToList();
            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync()).ToList();

            return Result<IEnumerable<SalaryGradeDto>>.Success(grades
                .OrderBy(g => g.Code)
                .Select(g => new SalaryGradeDto
                {
                    Id = g.Id, Name = g.Name, Code = g.Code,
                    BasicSalary = g.BasicSalary, IsActive = g.IsActive,
                    EmployeeCount = infos.Count(i => i.SalaryGradeId == g.Id)
                }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetGradesAsync", ex);
            return Result<IEnumerable<SalaryGradeDto>>.Failure("Could not load salary grades.");
        }
    }

    public async Task<Result> SaveGradeAsync(SaveSalaryGradeDto dto)
    {
        try
        {
            var code = dto.Code.Trim();
            if ((await _uow.SalaryGrades.FindAsync(g => g.Code == code && g.Id != dto.Id)).Any())
                return Result.Failure($"Grade code '{code}' is already used.");

            if (dto.Id == 0)
            {
                var grade = new SalaryGrade
                {
                    Name = dto.Name.Trim(), Code = code,
                    BasicSalary = dto.BasicSalary, IsActive = dto.IsActive
                };
                Stamp(grade, true);
                await _uow.SalaryGrades.AddAsync(grade);
            }
            else
            {
                var grade = await _uow.SalaryGrades.GetByIdAsync(dto.Id);
                if (grade == null) return Result.Failure("Grade not found.");

                // Recorded in the trail because this is a pay change for everyone on the
                // grade, not an edit to a label.
                var before = AuditSnapshot.Snapshot(grade);

                grade.Name = dto.Name.Trim(); grade.Code = code;
                grade.BasicSalary = dto.BasicSalary; grade.IsActive = dto.IsActive;
                Stamp(grade, false);
                await _uow.SalaryGrades.UpdateAsync(grade);
                await _uow.SaveChangesAsync();

                await _audit.LogAsync(Module, "UpdateGrade", _currentUser.UserId,
                    nameof(SalaryGrade), dto.Id, before, AuditSnapshot.Snapshot(grade));
                return Result.Success();
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "CreateGrade", _currentUser.UserId, nameof(SalaryGrade), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveGradeAsync", ex);
            return Result.Failure("Could not save the grade.");
        }
    }

    public async Task<Result> DeleteGradeAsync(int id)
    {
        try
        {
            if ((await _uow.EmployeePayrollInfos.FindAsync(e => e.SalaryGradeId == id)).Any())
                return Result.Failure("Employees are on this grade. Move them to another grade first.");

            await _uow.SalaryGrades.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteGrade", _currentUser.UserId, nameof(SalaryGrade), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteGradeAsync", ex);
            return Result.Failure("Could not delete the grade.");
        }
    }

    // ── Salary groups ─────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<SalaryGroupDto>>> GetGroupsAsync()
    {
        try
        {
            var groups = (await _uow.SalaryGroups.GetAllAsync()).ToList();
            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync()).ToList();

            return Result<IEnumerable<SalaryGroupDto>>.Success(groups
                .OrderBy(g => g.Name)
                .Select(g => new SalaryGroupDto
                {
                    Id = g.Id, Name = g.Name, Description = g.Description, IsActive = g.IsActive,
                    EmployeeCount = infos.Count(i => i.SalaryGroupId == g.Id)
                }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetGroupsAsync", ex);
            return Result<IEnumerable<SalaryGroupDto>>.Failure("Could not load salary groups.");
        }
    }

    public async Task<Result> SaveGroupAsync(SaveSalaryGroupDto dto)
    {
        try
        {
            if (dto.Id == 0)
            {
                var group = new SalaryGroup
                {
                    Name = dto.Name.Trim(), Description = dto.Description?.Trim(), IsActive = dto.IsActive
                };
                Stamp(group, true);
                await _uow.SalaryGroups.AddAsync(group);
            }
            else
            {
                var group = await _uow.SalaryGroups.GetByIdAsync(dto.Id);
                if (group == null) return Result.Failure("Group not found.");
                group.Name = dto.Name.Trim(); group.Description = dto.Description?.Trim();
                group.IsActive = dto.IsActive;
                Stamp(group, false);
                await _uow.SalaryGroups.UpdateAsync(group);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateGroup" : "UpdateGroup",
                _currentUser.UserId, nameof(SalaryGroup), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveGroupAsync", ex);
            return Result.Failure("Could not save the group.");
        }
    }

    public async Task<Result> DeleteGroupAsync(int id)
    {
        try
        {
            if ((await _uow.EmployeePayrollInfos.FindAsync(e => e.SalaryGroupId == id)).Any())
                return Result.Failure("Employees are in this group. Move them first.");

            await _uow.SalaryGroups.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteGroup", _currentUser.UserId, nameof(SalaryGroup), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteGroupAsync", ex);
            return Result.Failure("Could not delete the group.");
        }
    }

    // ── Sub-departments ───────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<SubDepartmentDto>>> GetSubDepartmentsAsync(int? departmentId)
    {
        try
        {
            var departments = (await _uow.Departments.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync()).ToList();

            var subs = (await _uow.SubDepartments.GetAllAsync())
                .Where(s => departmentId == null || s.DepartmentId == departmentId)
                .ToList();

            return Result<IEnumerable<SubDepartmentDto>>.Success(subs
                .OrderBy(s => departments.TryGetValue(s.DepartmentId, out var d) ? d : "")
                .ThenBy(s => s.Name)
                .Select(s => new SubDepartmentDto
                {
                    Id = s.Id, DepartmentId = s.DepartmentId,
                    DepartmentName = departments.TryGetValue(s.DepartmentId, out var dn) ? dn : string.Empty,
                    Name = s.Name, IsActive = s.IsActive,
                    EmployeeCount = infos.Count(i => i.SubDepartmentId == s.Id)
                }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetSubDepartmentsAsync", ex);
            return Result<IEnumerable<SubDepartmentDto>>.Failure("Could not load sub-departments.");
        }
    }

    public async Task<Result> SaveSubDepartmentAsync(SaveSubDepartmentDto dto)
    {
        try
        {
            var name = dto.Name.Trim();
            var clash = (await _uow.SubDepartments.FindAsync(
                s => s.DepartmentId == dto.DepartmentId && s.Name == name && s.Id != dto.Id)).Any();
            if (clash) return Result.Failure($"'{name}' already exists in this department.");

            if (dto.Id == 0)
            {
                var sub = new SubDepartment
                {
                    DepartmentId = dto.DepartmentId, Name = name, IsActive = dto.IsActive
                };
                Stamp(sub, true);
                await _uow.SubDepartments.AddAsync(sub);
            }
            else
            {
                var sub = await _uow.SubDepartments.GetByIdAsync(dto.Id);
                if (sub == null) return Result.Failure("Sub-department not found.");
                sub.DepartmentId = dto.DepartmentId; sub.Name = name; sub.IsActive = dto.IsActive;
                Stamp(sub, false);
                await _uow.SubDepartments.UpdateAsync(sub);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateSubDepartment" : "UpdateSubDepartment",
                _currentUser.UserId, nameof(SubDepartment), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveSubDepartmentAsync", ex);
            return Result.Failure("Could not save the sub-department.");
        }
    }

    public async Task<Result> DeleteSubDepartmentAsync(int id)
    {
        try
        {
            if ((await _uow.EmployeePayrollInfos.FindAsync(e => e.SubDepartmentId == id)).Any())
                return Result.Failure("Employees are in this sub-department. Move them first.");

            await _uow.SubDepartments.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteSubDepartment", _currentUser.UserId, nameof(SubDepartment), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteSubDepartmentAsync", ex);
            return Result.Failure("Could not delete the sub-department.");
        }
    }

    // ── Salary components ─────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<SalaryComponentDto>>> GetComponentsAsync()
    {
        try
        {
            var components = (await _uow.SalaryComponents.GetAllAsync())
                .OrderBy(c => c.ComponentType).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c => new SalaryComponentDto
                {
                    Id = c.Id, Name = c.Name, Code = c.Code, ComponentType = c.ComponentType,
                    Recurrence = c.Recurrence, IsEpfLiable = c.IsEpfLiable, IsApitLiable = c.IsApitLiable,
                    IncludeInOtRate = c.IncludeInOtRate, IncludeInGrossPay = c.IncludeInGrossPay,
                    BasedOnWorkingDays = c.BasedOnWorkingDays, IncludeInNoPay = c.IncludeInNoPay,
                    IncludeInAllowanceOnlyNoPay = c.IncludeInAllowanceOnlyNoPay,
                    CalculationType = c.CalculationType,
                    DefaultValue = c.DefaultValue, SortOrder = c.SortOrder, IsActive = c.IsActive
                });

            return Result<IEnumerable<SalaryComponentDto>>.Success(components);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetComponentsAsync", ex);
            return Result<IEnumerable<SalaryComponentDto>>.Failure("Could not load salary components.");
        }
    }

    public async Task<Result> SaveComponentAsync(SaveSalaryComponentDto dto)
    {
        try
        {
            var code = dto.Code.Trim().ToUpperInvariant();
            if ((await _uow.SalaryComponents.FindAsync(c => c.Code == code && c.Id != dto.Id)).Any())
                return Result.Failure($"Component code '{code}' is already used.");

            // A deduction cannot add to EPF-liable earnings — that would increase the
            // contribution by taking money away, which no payroll means.
            var isEpfLiable = dto.ComponentType == Domain.Enums.SalaryComponentType.Earning
                              && dto.IsEpfLiable;

            if (dto.Id == 0)
            {
                var c = new SalaryComponent
                {
                    Name = dto.Name.Trim(), Code = code, ComponentType = dto.ComponentType,
                    Recurrence = dto.Recurrence, IsEpfLiable = isEpfLiable, IsApitLiable = dto.IsApitLiable,
                    IncludeInOtRate = dto.IncludeInOtRate, IncludeInGrossPay = dto.IncludeInGrossPay,
                    BasedOnWorkingDays = dto.BasedOnWorkingDays, IncludeInNoPay = dto.IncludeInNoPay,
                    IncludeInAllowanceOnlyNoPay = dto.IncludeInAllowanceOnlyNoPay,
                    CalculationType = dto.CalculationType,
                    DefaultValue = dto.DefaultValue, SortOrder = dto.SortOrder, IsActive = dto.IsActive
                };
                Stamp(c, true);
                await _uow.SalaryComponents.AddAsync(c);
            }
            else
            {
                var c = await _uow.SalaryComponents.GetByIdAsync(dto.Id);
                if (c == null) return Result.Failure("Component not found.");

                var before = AuditSnapshot.Snapshot(c);

                c.Name = dto.Name.Trim(); c.Code = code; c.ComponentType = dto.ComponentType;
                c.Recurrence = dto.Recurrence; c.IsEpfLiable = isEpfLiable; c.IsApitLiable = dto.IsApitLiable;
                c.IncludeInOtRate = dto.IncludeInOtRate; c.IncludeInGrossPay = dto.IncludeInGrossPay;
                c.BasedOnWorkingDays = dto.BasedOnWorkingDays; c.IncludeInNoPay = dto.IncludeInNoPay;
                c.IncludeInAllowanceOnlyNoPay = dto.IncludeInAllowanceOnlyNoPay;
                c.CalculationType = dto.CalculationType;
                c.DefaultValue = dto.DefaultValue; c.SortOrder = dto.SortOrder; c.IsActive = dto.IsActive;
                Stamp(c, false);
                await _uow.SalaryComponents.UpdateAsync(c);
                await _uow.SaveChangesAsync();

                await _audit.LogAsync(Module, "UpdateComponent", _currentUser.UserId,
                    nameof(SalaryComponent), dto.Id, before, AuditSnapshot.Snapshot(c));
                return Result.Success();
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "CreateComponent", _currentUser.UserId,
                nameof(SalaryComponent), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveComponentAsync", ex);
            return Result.Failure("Could not save the component.");
        }
    }

    public async Task<Result> DeleteComponentAsync(int id)
    {
        try
        {
            if ((await _uow.EmployeeSalaryComponents.FindAsync(e => e.SalaryComponentId == id)).Any())
                return Result.Failure("Employees have a value set for this component. Remove those first.");

            await _uow.SalaryComponents.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteComponent", _currentUser.UserId, nameof(SalaryComponent), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteComponentAsync", ex);
            return Result.Failure("Could not delete the component.");
        }
    }

    // ── Employment categories ─────────────────────────────────────────────────

    public async Task<Result<IEnumerable<EmploymentCategoryDto>>> GetCategoriesAsync()
    {
        try
        {
            var categories = (await _uow.EmploymentCategories.GetAllAsync()).ToList();
            var infos = (await _uow.EmployeePayrollInfos.GetAllAsync()).ToList();

            return Result<IEnumerable<EmploymentCategoryDto>>.Success(categories
                .OrderBy(c => c.Name)
                .Select(c => new EmploymentCategoryDto
                {
                    Id = c.Id, Name = c.Name, Code = c.Code,
                    IsEpfEligible = c.IsEpfEligible, IsActive = c.IsActive,
                    EmployeeCount = infos.Count(i => i.EmploymentCategoryId == c.Id)
                }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetCategoriesAsync", ex);
            return Result<IEnumerable<EmploymentCategoryDto>>.Failure("Could not load employment categories.");
        }
    }

    public async Task<Result> SaveCategoryAsync(SaveEmploymentCategoryDto dto)
    {
        try
        {
            var code = dto.Code.Trim().ToUpperInvariant();
            if ((await _uow.EmploymentCategories.FindAsync(c => c.Code == code && c.Id != dto.Id)).Any())
                return Result.Failure($"Category code '{code}' is already used.");

            if (dto.Id == 0)
            {
                var c = new EmploymentCategory
                {
                    Name = dto.Name.Trim(), Code = code,
                    IsEpfEligible = dto.IsEpfEligible, IsActive = dto.IsActive
                };
                Stamp(c, true);
                await _uow.EmploymentCategories.AddAsync(c);
            }
            else
            {
                var c = await _uow.EmploymentCategories.GetByIdAsync(dto.Id);
                if (c == null) return Result.Failure("Category not found.");
                c.Name = dto.Name.Trim(); c.Code = code;
                c.IsEpfEligible = dto.IsEpfEligible; c.IsActive = dto.IsActive;
                Stamp(c, false);
                await _uow.EmploymentCategories.UpdateAsync(c);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateCategory" : "UpdateCategory",
                _currentUser.UserId, nameof(EmploymentCategory), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveCategoryAsync", ex);
            return Result.Failure("Could not save the category.");
        }
    }

    public async Task<Result> DeleteCategoryAsync(int id)
    {
        try
        {
            if ((await _uow.EmployeePayrollInfos.FindAsync(e => e.EmploymentCategoryId == id)).Any())
                return Result.Failure("Employees are in this category. Move them first.");

            await _uow.EmploymentCategories.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteCategory", _currentUser.UserId,
                nameof(EmploymentCategory), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteCategoryAsync", ex);
            return Result.Failure("Could not delete the category.");
        }
    }

    // ── Loan types ────────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<LoanTypeDto>>> GetLoanTypesAsync()
    {
        try
        {
            var types = (await _uow.LoanTypes.GetAllAsync())
                .OrderBy(t => t.Code)
                .Select(t => new LoanTypeDto
                {
                    Id = t.Id, Code = t.Code, Description = t.Description,
                    InterestType = t.InterestType, InterestRate = t.InterestRate,
                    IsActive = t.IsActive
                });

            return Result<IEnumerable<LoanTypeDto>>.Success(types);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetLoanTypesAsync", ex);
            return Result<IEnumerable<LoanTypeDto>>.Failure("Could not load loan types.");
        }
    }

    public async Task<Result> SaveLoanTypeAsync(SaveLoanTypeDto dto)
    {
        try
        {
            var code = dto.Code.Trim().ToUpperInvariant();
            if ((await _uow.LoanTypes.FindAsync(t => t.Code == code && t.Id != dto.Id)).Any())
                return Result.Failure($"Loan type code '{code}' is already used.");

            if (dto.Id == 0)
            {
                var t = new LoanType
                {
                    Code = code, Description = dto.Description.Trim(),
                    InterestType = dto.InterestType, InterestRate = dto.InterestRate,
                    IsActive = dto.IsActive
                };
                Stamp(t, true);
                await _uow.LoanTypes.AddAsync(t);
            }
            else
            {
                var t = await _uow.LoanTypes.GetByIdAsync(dto.Id);
                if (t == null) return Result.Failure("Loan type not found.");

                // Recorded with before and after: the rate and the interest model decide what
                // borrowers repay, so a change here is worth being able to trace.
                var before = AuditSnapshot.Snapshot(t);

                t.Code = code; t.Description = dto.Description.Trim();
                t.InterestType = dto.InterestType; t.InterestRate = dto.InterestRate;
                t.IsActive = dto.IsActive;
                Stamp(t, false);
                await _uow.LoanTypes.UpdateAsync(t);
                await _uow.SaveChangesAsync();

                await _audit.LogAsync(Module, "UpdateLoanType", _currentUser.UserId,
                    nameof(LoanType), dto.Id, before, AuditSnapshot.Snapshot(t));
                return Result.Success();
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "CreateLoanType", _currentUser.UserId, nameof(LoanType), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveLoanTypeAsync", ex);
            return Result.Failure("Could not save the loan type.");
        }
    }

    public async Task<Result> DeleteLoanTypeAsync(int id)
    {
        try
        {
            await _uow.LoanTypes.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteLoanType", _currentUser.UserId, nameof(LoanType), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteLoanTypeAsync", ex);
            return Result.Failure("Could not delete the loan type.");
        }
    }

    // ── Branch payroll parameters ─────────────────────────────────────────────

    public async Task<Result<BranchPayrollSettingsDto>> GetBranchSettingsAsync(int branchId)
    {
        try
        {
            var branch = await _uow.Branches.GetByIdAsync(branchId);
            if (branch == null) return Result<BranchPayrollSettingsDto>.Failure("Branch not found.");

            var s = (await _uow.BranchPayrollSettings.FindAsync(x => x.BranchId == branchId))
                .FirstOrDefault();

            // Defaults returned for a branch that has never been configured, so the screen
            // opens on sensible values rather than zeros that look deliberate.
            var dto = s == null
                ? new BranchPayrollSettingsDto { BranchId = branchId, IsNew = true }
                : new BranchPayrollSettingsDto
                {
                    Id = s.Id, BranchId = branchId,
                    EpfDCode = s.EpfDCode,
                    EpfContactPerson = s.EpfContactPerson,
                    EpfContactPhone = s.EpfContactPhone,
                    PayeRegistrationNo = s.PayeRegistrationNo,
                    NonCitizenTaxYears = s.NonCitizenTaxYears,
                    EmployeeEpfPercent = s.EmployeeEpfPercent,
                    EmployerEpfPercent = s.EmployerEpfPercent,
                    EmployerEtfPercent = s.EmployerEtfPercent,
                    DaysPerMonth = s.DaysPerMonth,
                    HoursPerDay = s.HoursPerDay,
                    BankBranchId = s.BankBranchId,
                    AccountNumber = s.AccountNumber,
                    GratuityPercentOfBasic = s.GratuityPercentOfBasic,
                    GratuityQualifyingYears = s.GratuityQualifyingYears,
                    RoundOffSalaryPayable = s.RoundOffSalaryPayable,
                    RoundNearest = s.RoundNearest,
                    CarryForwardMinusSalary = s.CarryForwardMinusSalary,
                    CarryForwardCoins = s.CarryForwardCoins,
                    EpfRounding = s.EpfRounding,
                    EtfRounding = s.EtfRounding,
                    NoPayRounding = s.NoPayRounding,
                    TaxRounding = s.TaxRounding,
                    LoanRounding = s.LoanRounding,
                    OvertimeRounding = s.OvertimeRounding
                };

            dto.BranchName = branch.Name;

            // These two live on Branch itself. Surfaced here because this is the screen where
            // somebody sets up a branch for payroll, and sending them elsewhere for two fields
            // is how one of them ends up blank.
            dto.EpfEmployerNumber = branch.EpfEmployerNumber;
            dto.EtfEmployerNumber = branch.EtfEmployerNumber;

            if (dto.BankBranchId.HasValue)
            {
                var bb = await _uow.BankBranches.GetByIdAsync(dto.BankBranchId.Value);
                dto.BankBranchName = bb?.Name;
                if (bb != null) dto.BankName = (await _uow.Banks.GetByIdAsync(bb.BankId))?.Name;
            }

            // Named so a blank percentage box says what it falls back to.
            var rate = (await _uow.EpfEtfRates.GetAllAsync())
                .Where(r => r.EffectiveFrom.Date <= DateTime.Today)
                .OrderByDescending(r => r.EffectiveFrom)
                .FirstOrDefault();

            if (rate != null)
            {
                dto.CompanyEmployeeEpfPercent = rate.EmployeeEpfPercent;
                dto.CompanyEmployerEpfPercent = rate.EmployerEpfPercent;
                dto.CompanyEmployerEtfPercent = rate.EmployerEtfPercent;
            }

            return Result<BranchPayrollSettingsDto>.Success(dto);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetBranchSettingsAsync", ex);
            return Result<BranchPayrollSettingsDto>.Failure("Could not load the branch parameters.");
        }
    }

    public async Task<Result> SaveBranchSettingsAsync(SaveBranchPayrollSettingsDto dto)
    {
        try
        {
            var branch = await _uow.Branches.GetByIdAsync(dto.BranchId);
            if (branch == null) return Result.Failure("Branch not found.");

            // The registration numbers belong to the branch, so they are written there rather
            // than duplicated onto the parameters row.
            branch.EpfEmployerNumber = dto.EpfEmployerNumber?.Trim();
            branch.EtfEmployerNumber = dto.EtfEmployerNumber?.Trim();
            branch.ModifiedBy = _currentUser.UserId;
            branch.ModifiedAt = DateTime.Now;
            await _uow.Branches.UpdateAsync(branch);

            var s = (await _uow.BranchPayrollSettings.FindAsync(x => x.BranchId == dto.BranchId))
                .FirstOrDefault();

            var isNew = s == null;
            var before = isNew ? null : AuditSnapshot.Snapshot(s);

            s ??= new BranchPayrollSettings { BranchId = dto.BranchId };

            s.EpfDCode = dto.EpfDCode?.Trim();
            s.EpfContactPerson = dto.EpfContactPerson?.Trim();
            s.EpfContactPhone = dto.EpfContactPhone?.Trim();
            s.PayeRegistrationNo = dto.PayeRegistrationNo?.Trim();
            s.NonCitizenTaxYears = dto.NonCitizenTaxYears;
            s.EmployeeEpfPercent = dto.EmployeeEpfPercent;
            s.EmployerEpfPercent = dto.EmployerEpfPercent;
            s.EmployerEtfPercent = dto.EmployerEtfPercent;
            s.DaysPerMonth = dto.DaysPerMonth;
            s.HoursPerDay = dto.HoursPerDay;
            s.BankBranchId = dto.BankBranchId;
            s.AccountNumber = dto.AccountNumber?.Trim();
            s.GratuityPercentOfBasic = dto.GratuityPercentOfBasic;
            s.GratuityQualifyingYears = dto.GratuityQualifyingYears;
            s.RoundOffSalaryPayable = dto.RoundOffSalaryPayable;
            s.RoundNearest = dto.RoundNearest <= 0 ? 1m : dto.RoundNearest;
            s.CarryForwardMinusSalary = dto.CarryForwardMinusSalary;
            s.CarryForwardCoins = dto.CarryForwardCoins;
            s.EpfRounding = dto.EpfRounding;
            s.EtfRounding = dto.EtfRounding;
            s.NoPayRounding = dto.NoPayRounding;
            s.TaxRounding = dto.TaxRounding;
            s.LoanRounding = dto.LoanRounding;
            s.OvertimeRounding = dto.OvertimeRounding;

            Stamp(s, isNew);
            if (isNew) await _uow.BranchPayrollSettings.AddAsync(s);
            else await _uow.BranchPayrollSettings.UpdateAsync(s);

            await _uow.SaveChangesAsync();

            // Rates, rounding and the days divisor all move what people are paid, so before
            // and after are both recorded.
            await _audit.LogAsync(Module, isNew ? "CreateBranchParameters" : "UpdateBranchParameters",
                _currentUser.UserId, nameof(BranchPayrollSettings), s.Id,
                before, AuditSnapshot.Snapshot(s));

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveBranchSettingsAsync", ex);
            return Result.Failure("Could not save the branch parameters.");
        }
    }

    // ── Third-party deductions ────────────────────────────────────────────────

    public async Task<Result<IEnumerable<ThirdPartyDto>>> GetThirdPartiesAsync()
    {
        try
        {
            var components = (await _uow.SalaryComponents.GetAllAsync()).ToDictionary(c => c.Id);

            var parties = (await _uow.ThirdParties.GetAllAsync())
                .OrderBy(p => p.CompanyName)
                .Select(p =>
                {
                    var dto = new ThirdPartyDto
                    {
                        Id = p.Id, Code = p.Code, CompanyName = p.CompanyName,
                        Address = p.Address, SalaryComponentId = p.SalaryComponentId,
                        IsActive = p.IsActive
                    };

                    if (p.SalaryComponentId != null &&
                        components.TryGetValue(p.SalaryComponentId.Value, out var c))
                    {
                        dto.DeductionCode = c.Code;
                        dto.DeductionName = c.Name;
                    }

                    return dto;
                });

            return Result<IEnumerable<ThirdPartyDto>>.Success(parties);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetThirdPartiesAsync", ex);
            return Result<IEnumerable<ThirdPartyDto>>.Failure("Could not load third parties.");
        }
    }

    public async Task<Result> SaveThirdPartyAsync(SaveThirdPartyDto dto)
    {
        try
        {
            var code = dto.Code.Trim().ToUpperInvariant();
            if ((await _uow.ThirdParties.FindAsync(p => p.Code == code && p.Id != dto.Id)).Any())
                return Result.Failure($"Third party code '{code}' is already used.");

            // Refused rather than accepted quietly: pointing a payee at an earning would
            // collect nothing, and the mistake would only surface when a remittance came out
            // empty.
            if (dto.SalaryComponentId.HasValue)
            {
                var component = await _uow.SalaryComponents.GetByIdAsync(dto.SalaryComponentId.Value);
                if (component == null) return Result.Failure("That deduction no longer exists.");

                if (component.ComponentType != Domain.Enums.SalaryComponentType.Deduction)
                    return Result.Failure(
                        $"'{component.Name}' is an earning, not a deduction — nothing would be " +
                        "collected for this party.");
            }

            if (dto.Id == 0)
            {
                var p = new ThirdParty
                {
                    Code = code, CompanyName = dto.CompanyName.Trim(),
                    Address = dto.Address?.Trim(), SalaryComponentId = dto.SalaryComponentId,
                    IsActive = dto.IsActive
                };
                Stamp(p, true);
                await _uow.ThirdParties.AddAsync(p);
            }
            else
            {
                var p = await _uow.ThirdParties.GetByIdAsync(dto.Id);
                if (p == null) return Result.Failure("Third party not found.");
                p.Code = code; p.CompanyName = dto.CompanyName.Trim();
                p.Address = dto.Address?.Trim(); p.SalaryComponentId = dto.SalaryComponentId;
                p.IsActive = dto.IsActive;
                Stamp(p, false);
                await _uow.ThirdParties.UpdateAsync(p);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateThirdParty" : "UpdateThirdParty",
                _currentUser.UserId, nameof(ThirdParty), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveThirdPartyAsync", ex);
            return Result.Failure("Could not save the third party.");
        }
    }

    public async Task<Result> DeleteThirdPartyAsync(int id)
    {
        try
        {
            await _uow.ThirdParties.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteThirdParty", _currentUser.UserId, nameof(ThirdParty), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteThirdPartyAsync", ex);
            return Result.Failure("Could not delete the third party.");
        }
    }

    // ── APIT tables ───────────────────────────────────────────────────────────

    /// <summary>
    /// The IRD's own wording for each schedule. Kept here rather than in the browser so the
    /// name is the same on a screen, an export and a log line.
    /// </summary>
    private static string TaxTableTypeName(TaxTableType type) => type switch
    {
        TaxTableType.Monthly => "Monthly Tax Table",
        TaxTableType.Bonus => "Bonus Tax Table",
        TaxTableType.NonCitizen => "Non Citizen Tax Table",
        TaxTableType.Yearly => "Yearly Tax Table",
        TaxTableType.TaxOnTax => "Tax on Tax Table",
        _ => type.ToString()
    };

    public async Task<Result<IEnumerable<ApitTaxTableDto>>> GetApitTablesAsync()
    {
        try
        {
            var tables = (await _uow.ApitTaxTables.GetAllAsync()).ToList();
            var bands = (await _uow.ApitTaxBrackets.GetAllAsync()).ToList();

            // Grouped by type first: the five schedules are separate things, and a flat list
            // sorted by code would interleave a bonus table between two monthly ones.
            return Result<IEnumerable<ApitTaxTableDto>>.Success(tables
                .OrderBy(t => t.TableType)
                .ThenByDescending(t => t.IsDefault).ThenBy(t => t.Code)
                .Select(t => new ApitTaxTableDto
                {
                    Id = t.Id, Name = t.Name, Code = t.Code, Description = t.Description,
                    TableType = t.TableType,
                    TableTypeDisplay = TaxTableTypeName(t.TableType),
                    IsDefault = t.IsDefault, IsActive = t.IsActive,
                    BandCount = bands.Count(b => b.ApitTaxTableId == t.Id)
                }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetApitTablesAsync", ex);
            return Result<IEnumerable<ApitTaxTableDto>>.Failure("Could not load the tax tables.");
        }
    }

    public async Task<Result> SaveApitTableAsync(SaveApitTaxTableDto dto)
    {
        try
        {
            var code = dto.Code.Trim().ToUpperInvariant();
            if ((await _uow.ApitTaxTables.FindAsync(t => t.Code == code && t.Id != dto.Id)).Any())
                return Result.Failure($"Table code '{code}' is already used.");

            ApitTaxTable table;
            if (dto.Id == 0)
            {
                table = new ApitTaxTable();
                Stamp(table, true);
            }
            else
            {
                table = (await _uow.ApitTaxTables.GetByIdAsync(dto.Id))!;
                if (table == null) return Result.Failure("Tax table not found.");
                Stamp(table, false);
            }

            table.Name = dto.Name.Trim(); table.Code = code;
            table.Description = dto.Description?.Trim();
            table.TableType = dto.TableType;
            table.IsDefault = dto.IsDefault; table.IsActive = dto.IsActive;

            if (dto.Id == 0) await _uow.ApitTaxTables.AddAsync(table);
            else await _uow.ApitTaxTables.UpdateAsync(table);

            await _uow.SaveChangesAsync();

            // One default PER TYPE, not one overall. Two within a type would make "which
            // table applies to an employee with none assigned" ambiguous and the answer would
            // depend on row order; clearing across types would instead leave the bonus and
            // non-citizen schedules with no fallback the moment a monthly one was made
            // default.
            if (dto.IsDefault)
            {
                foreach (var other in await _uow.ApitTaxTables.FindAsync(
                             t => t.IsDefault && t.TableType == table.TableType && t.Id != table.Id))
                {
                    other.IsDefault = false;
                    await _uow.ApitTaxTables.UpdateAsync(other);
                }
                await _uow.SaveChangesAsync();
            }

            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateApitTable" : "UpdateApitTable",
                _currentUser.UserId, nameof(ApitTaxTable), table.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveApitTableAsync", ex);
            return Result.Failure("Could not save the tax table.");
        }
    }

    // ── Statutory rates ───────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<EpfEtfRateDto>>> GetRatesAsync()
    {
        try
        {
            var rates = (await _uow.EpfEtfRates.GetAllAsync())
                .OrderByDescending(r => r.EffectiveFrom).ToList();

            // The row in force today is the most recent one that has already started.
            var current = rates.FirstOrDefault(r => r.EffectiveFrom.Date <= DateTime.Today);

            return Result<IEnumerable<EpfEtfRateDto>>.Success(rates.Select(r => new EpfEtfRateDto
            {
                Id = r.Id, EffectiveFrom = r.EffectiveFrom,
                EmployeeEpfPercent = r.EmployeeEpfPercent,
                EmployerEpfPercent = r.EmployerEpfPercent,
                EmployerEtfPercent = r.EmployerEtfPercent,
                Notes = r.Notes,
                IsCurrent = current != null && r.Id == current.Id
            }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetRatesAsync", ex);
            return Result<IEnumerable<EpfEtfRateDto>>.Failure("Could not load EPF/ETF rates.");
        }
    }

    public async Task<Result> SaveRateAsync(SaveEpfEtfRateDto dto)
    {
        try
        {
            var from = dto.EffectiveFrom.Date;
            if ((await _uow.EpfEtfRates.FindAsync(r => r.EffectiveFrom == from && r.Id != dto.Id)).Any())
                return Result.Failure($"A rate already starts on {from:dd-MMM-yyyy}.");

            if (dto.Id == 0)
            {
                var rate = new EpfEtfRate
                {
                    EffectiveFrom = from,
                    EmployeeEpfPercent = dto.EmployeeEpfPercent,
                    EmployerEpfPercent = dto.EmployerEpfPercent,
                    EmployerEtfPercent = dto.EmployerEtfPercent,
                    Notes = dto.Notes?.Trim()
                };
                Stamp(rate, true);
                await _uow.EpfEtfRates.AddAsync(rate);
            }
            else
            {
                var rate = await _uow.EpfEtfRates.GetByIdAsync(dto.Id);
                if (rate == null) return Result.Failure("Rate not found.");
                rate.EffectiveFrom = from;
                rate.EmployeeEpfPercent = dto.EmployeeEpfPercent;
                rate.EmployerEpfPercent = dto.EmployerEpfPercent;
                rate.EmployerEtfPercent = dto.EmployerEtfPercent;
                rate.Notes = dto.Notes?.Trim();
                Stamp(rate, false);
                await _uow.EpfEtfRates.UpdateAsync(rate);
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateEpfRate" : "UpdateEpfRate",
                _currentUser.UserId, nameof(EpfEtfRate), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveRateAsync", ex);
            return Result.Failure("Could not save the rate.");
        }
    }

    public async Task<Result<IEnumerable<ApitBracketDto>>> GetApitBracketsAsync()
    {
        try
        {
            var tables = (await _uow.ApitTaxTables.GetAllAsync()).ToDictionary(t => t.Id, t => t.Name);

            var brackets = (await _uow.ApitTaxBrackets.GetAllAsync())
                // Grouped by table first: bands only mean anything within one, and interleaving
                // two tables' bands by date makes the ladder impossible to read.
                .OrderBy(b => b.ApitTaxTableId)
                .ThenByDescending(b => b.EffectiveFrom).ThenBy(b => b.SortOrder)
                .Select(b => new ApitBracketDto
                {
                    Id = b.Id,
                    ApitTaxTableId = b.ApitTaxTableId,
                    TaxTableName = tables.TryGetValue(b.ApitTaxTableId, out var n) ? n : string.Empty,
                    EffectiveFrom = b.EffectiveFrom,
                    FromAmount = b.FromAmount, ToAmount = b.ToAmount,
                    Rate = b.Rate, Relief = b.Relief, SortOrder = b.SortOrder
                });

            return Result<IEnumerable<ApitBracketDto>>.Success(brackets);
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.GetApitBracketsAsync", ex);
            return Result<IEnumerable<ApitBracketDto>>.Failure("Could not load the APIT table.");
        }
    }

    public async Task<Result> SaveApitBracketAsync(SaveApitBracketDto dto)
    {
        try
        {
            if (dto.ToAmount.HasValue && dto.ToAmount <= dto.FromAmount)
                return Result.Failure("The upper bound must be greater than the lower bound.");

            if (await _uow.ApitTaxTables.GetByIdAsync(dto.ApitTaxTableId) == null)
                return Result.Failure("That tax table no longer exists.");

            // Overlapping bands make the tax on an amount depend on which row is read first.
            // Checked within the table and effective date, since two tables legitimately
            // cover the same ranges.
            var siblings = (await _uow.ApitTaxBrackets.FindAsync(
                    x => x.ApitTaxTableId == dto.ApitTaxTableId
                      && x.EffectiveFrom == dto.EffectiveFrom.Date
                      && x.Id != dto.Id))
                .ToList();

            var overlap = siblings.FirstOrDefault(x =>
                dto.FromAmount < (x.ToAmount ?? decimal.MaxValue) &&
                (dto.ToAmount ?? decimal.MaxValue) > x.FromAmount);

            if (overlap != null)
                return Result.Failure(
                    $"This overlaps an existing band ({overlap.FromAmount:N0} – " +
                    $"{(overlap.ToAmount.HasValue ? overlap.ToAmount.Value.ToString("N0") : "above")}). " +
                    "Bands must not overlap, or the tax on an amount would depend on row order.");

            if (dto.Id == 0)
            {
                var b = new ApitTaxBracket
                {
                    ApitTaxTableId = dto.ApitTaxTableId,
                    EffectiveFrom = dto.EffectiveFrom.Date, FromAmount = dto.FromAmount,
                    ToAmount = dto.ToAmount, Rate = dto.Rate, Relief = dto.Relief,
                    SortOrder = dto.SortOrder
                };
                Stamp(b, true);
                await _uow.ApitTaxBrackets.AddAsync(b);
            }
            else
            {
                var b = await _uow.ApitTaxBrackets.GetByIdAsync(dto.Id);
                if (b == null) return Result.Failure("Band not found.");

                // Tax bands decide what everyone on the table pays, so before and after are
                // both recorded.
                var before = AuditSnapshot.Snapshot(b);

                b.ApitTaxTableId = dto.ApitTaxTableId;
                b.EffectiveFrom = dto.EffectiveFrom.Date; b.FromAmount = dto.FromAmount;
                b.ToAmount = dto.ToAmount; b.Rate = dto.Rate; b.Relief = dto.Relief;
                b.SortOrder = dto.SortOrder;
                Stamp(b, false);
                await _uow.ApitTaxBrackets.UpdateAsync(b);
                await _uow.SaveChangesAsync();

                await _audit.LogAsync(Module, "UpdateApitBand", _currentUser.UserId,
                    nameof(ApitTaxBracket), dto.Id, before, AuditSnapshot.Snapshot(b));
                return Result.Success();
            }

            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, dto.Id == 0 ? "CreateApitBand" : "UpdateApitBand",
                _currentUser.UserId, nameof(ApitTaxBracket), dto.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.SaveApitBracketAsync", ex);
            return Result.Failure("Could not save the band.");
        }
    }

    public async Task<Result> DeleteApitBracketAsync(int id)
    {
        try
        {
            await _uow.ApitTaxBrackets.DeleteAsync(id);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync(Module, "DeleteApitBand", _currentUser.UserId, nameof(ApitTaxBracket), id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("PayrollSetupService.DeleteApitBracketAsync", ex);
            return Result.Failure("Could not delete the band.");
        }
    }
}

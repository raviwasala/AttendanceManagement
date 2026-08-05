using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>Manages employees: CRUD, photo, code generation, search.</summary>
public class EmployeeService : IEmployeeService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;

    public EmployeeService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<IEnumerable<EmployeeListItemDto>>> GetAllAsync()
    {
        try
        {
            var employees = await _uow.Employees.GetActiveEmployeesAsync();
            return Result<IEnumerable<EmployeeListItemDto>>.Success(employees.Select(MapToListDto));
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeService.GetAllAsync", ex);
            return Result<IEnumerable<EmployeeListItemDto>>.Failure(ex.Message);
        }
    }

    public async Task<Result<PagedResult<EmployeeListItemDto>>> GetPagedAsync(
        string? search, int? departmentId, int? designationId, int? branchId,
        bool? isActive, PageRequest page)
    {
        try
        {
            var (items, total) = await _uow.Employees.GetPagedAsync(
                search, departmentId, designationId, branchId, isActive, page.Skip, page.PageSize);

            return Result<PagedResult<EmployeeListItemDto>>.Success(new PagedResult<EmployeeListItemDto>
            {
                Items = items.Select(MapToListDto).ToList(),
                Page = page.Page,
                PageSize = page.PageSize,
                TotalCount = total
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeService.GetPagedAsync", ex);
            return Result<PagedResult<EmployeeListItemDto>>.Failure("Failed to load employees.");
        }
    }

    public async Task<Result<EmployeeDto>> GetByIdAsync(int id)
    {
        try
        {
            var emp = await _uow.Employees.GetWithDetailsAsync(id);
            if (emp == null) return Result<EmployeeDto>.Failure("Employee not found.");
            return Result<EmployeeDto>.Success(MapToDto(emp));
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeService.GetByIdAsync", ex);
            return Result<EmployeeDto>.Failure(ex.Message);
        }
    }

    public async Task<Result<EmployeeDto>> SaveAsync(SaveEmployeeDto dto)
    {
        try
        {
            // Two employees sharing a device enrolment id would have each other's punches
            // attributed to whichever record the importer matched first — a payroll error
            // that is very hard to spot afterwards. Reject it at the point of entry.
            if (dto.BiometricEnrollId.HasValue)
            {
                if (dto.BiometricEnrollId.Value <= 0)
                    return Result<EmployeeDto>.Failure("Biometric enroll ID must be a positive number.");

                var clash = (await _uow.Employees.FindAsync(e =>
                    e.BiometricEnrollId == dto.BiometricEnrollId && e.Id != dto.Id && !e.IsDeleted))
                    .FirstOrDefault();

                if (clash != null)
                    return Result<EmployeeDto>.Failure(
                        $"Biometric enroll ID {dto.BiometricEnrollId} is already assigned to " +
                        $"{clash.FirstName} {clash.LastName} ({clash.EmployeeCode}).");
            }

            if (dto.Id == 0)
            {
                // Create
                var code = await _uow.Employees.GenerateNextCodeAsync();
                var emp = new Employee
                {
                    EmployeeCode = code,
                    UserCode = dto.UserCode?.Trim(),
                    NameWithInitials = dto.NameWithInitials?.Trim(),
                    Nic = dto.Nic?.Trim(),
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName?.Trim() ?? string.Empty,
                    Email = dto.Email?.Trim(),
                    Phone = dto.Phone?.Trim(),
                    DateOfBirth = dto.DateOfBirth,
                    JoiningDate = dto.JoiningDate,
                    Gender = dto.Gender,
                    Address = dto.Address?.Trim(),
                    Photo = dto.Photo is { Length: > 0 } ? dto.Photo : null,
                    DepartmentId = dto.DepartmentId,
                    DesignationId = dto.DesignationId,
                    BranchId = dto.BranchId,
                    IsActive = dto.IsActive,
                    BiometricEnrollId = dto.BiometricEnrollId,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                };
                await _uow.Employees.AddAsync(emp);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync("Employees", "Create", _currentUser.UserId, "Employee", emp.Id,
                    newValues: AuditSnapshot.Snapshot(emp));
                return await GetByIdAsync(emp.Id);
            }
            else
            {
                // Update
                var emp = await _uow.Employees.GetByIdAsync(dto.Id);
                if (emp == null) return Result<EmployeeDto>.Failure("Employee not found.");

                var before = AuditSnapshot.Capture(emp);

                emp.UserCode = dto.UserCode?.Trim();
                emp.NameWithInitials = dto.NameWithInitials?.Trim();
                emp.Nic = dto.Nic?.Trim();
                emp.FirstName = dto.FirstName.Trim();
                emp.LastName = dto.LastName?.Trim() ?? string.Empty;
                emp.Email = dto.Email?.Trim();
                emp.Phone = dto.Phone?.Trim();
                emp.DateOfBirth = dto.DateOfBirth;
                emp.JoiningDate = dto.JoiningDate;
                emp.Gender = dto.Gender;
                emp.Address = dto.Address?.Trim();
                // null means the caller is not talking about the photo, so leave it. An empty
                // array is a caller that is talking about it and says there isn't one — that
                // is how "Remove photo" reaches the database instead of being ignored.
                if (dto.Photo != null) emp.Photo = dto.Photo.Length > 0 ? dto.Photo : null;
                emp.DepartmentId = dto.DepartmentId;
                emp.DesignationId = dto.DesignationId;
                emp.BranchId = dto.BranchId;
                emp.IsActive = dto.IsActive;
                emp.BiometricEnrollId = dto.BiometricEnrollId;
                emp.ModifiedBy = _currentUser.UserId;
                emp.ModifiedAt = DateTime.Now;

                await _uow.Employees.UpdateAsync(emp);
                await _uow.SaveChangesAsync();

                var (oldValues, newValues) = AuditSnapshot.DiffAgainst(before, emp);
                await _audit.LogAsync("Employees", "Update", _currentUser.UserId, "Employee", emp.Id,
                    oldValues, newValues);
                return await GetByIdAsync(emp.Id);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeService.SaveAsync", ex);
            return Result<EmployeeDto>.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(int id, int deletedBy)
    {
        try
        {
            var emp = await _uow.Employees.GetByIdAsync(id);
            if (emp == null) return Result.Failure("Employee not found.");

            // Soft delete hides the row from every screen, so the audit entry is the only place
            // the record's contents remain visible.
            var deleted = AuditSnapshot.Snapshot(emp);

            emp.IsDeleted = true;
            emp.ModifiedBy = deletedBy;
            emp.ModifiedAt = DateTime.Now;
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Employees", "Delete", deletedBy, "Employee", id, deleted);
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeService.DeleteAsync", ex);
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result<IEnumerable<EmployeeListItemDto>>> SearchAsync(string keyword)
    {
        try
        {
            var employees = await _uow.Employees.SearchAsync(keyword);
            return Result<IEnumerable<EmployeeListItemDto>>.Success(employees.Select(MapToListDto));
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeService.SearchAsync", ex);
            return Result<IEnumerable<EmployeeListItemDto>>.Failure(ex.Message);
        }
    }

    public async Task<Result> ToggleActiveAsync(int id, int modifiedBy)
    {
        try
        {
            var emp = await _uow.Employees.GetByIdAsync(id);
            if (emp == null) return Result.Failure("Employee not found.");
            emp.IsActive = !emp.IsActive;
            emp.ModifiedBy = modifiedBy;
            emp.ModifiedAt = DateTime.Now;
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("EmployeeService.ToggleActiveAsync", ex);
            return Result.Failure(ex.Message);
        }
    }

    private static EmployeeDto MapToDto(Employee e) => new()
    {
        Id = e.Id, EmployeeCode = e.EmployeeCode, UserCode = e.UserCode,
        NameWithInitials = e.NameWithInitials, Nic = e.Nic,
        FirstName = e.FirstName,
        LastName = e.LastName, Email = e.Email, Phone = e.Phone,
        DateOfBirth = e.DateOfBirth, JoiningDate = e.JoiningDate, Gender = e.Gender,
        Address = e.Address, Photo = e.Photo, DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name ?? string.Empty, DesignationId = e.DesignationId,
        DesignationName = e.Designation?.Name ?? string.Empty, BranchId = e.BranchId,
        BranchName = e.Branch?.Name ?? string.Empty, IsActive = e.IsActive, CreatedAt = e.CreatedAt,
        BiometricEnrollId = e.BiometricEnrollId
    };

    private static EmployeeListItemDto MapToListDto(Employee e) => new()
    {
        Id = e.Id, EmployeeCode = e.EmployeeCode, UserCode = e.UserCode,
        NameWithInitials = e.NameWithInitials, Nic = e.Nic,
        FullName = $"{e.FirstName} {e.LastName}".Trim(),
        Department = e.Department?.Name ?? string.Empty,
        Designation = e.Designation?.Name ?? string.Empty,
        Branch = e.Branch?.Name ?? string.Empty,
        Phone = e.Phone, Email = e.Email, IsActive = e.IsActive,
        BiometricEnrollId = e.BiometricEnrollId
    };
}

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
            if (dto.Id == 0)
            {
                // Create
                var code = await _uow.Employees.GenerateNextCodeAsync();
                var emp = new Employee
                {
                    EmployeeCode = code,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    Email = dto.Email?.Trim(),
                    Phone = dto.Phone?.Trim(),
                    DateOfBirth = dto.DateOfBirth,
                    JoiningDate = dto.JoiningDate,
                    Gender = dto.Gender,
                    Address = dto.Address?.Trim(),
                    Photo = dto.Photo,
                    DepartmentId = dto.DepartmentId,
                    DesignationId = dto.DesignationId,
                    BranchId = dto.BranchId,
                    IsActive = dto.IsActive,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.Now
                };
                await _uow.Employees.AddAsync(emp);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync("Employees", "Create", _currentUser.UserId, "Employee", emp.Id);
                return await GetByIdAsync(emp.Id);
            }
            else
            {
                // Update
                var emp = await _uow.Employees.GetByIdAsync(dto.Id);
                if (emp == null) return Result<EmployeeDto>.Failure("Employee not found.");

                emp.FirstName = dto.FirstName.Trim();
                emp.LastName = dto.LastName.Trim();
                emp.Email = dto.Email?.Trim();
                emp.Phone = dto.Phone?.Trim();
                emp.DateOfBirth = dto.DateOfBirth;
                emp.JoiningDate = dto.JoiningDate;
                emp.Gender = dto.Gender;
                emp.Address = dto.Address?.Trim();
                if (dto.Photo != null) emp.Photo = dto.Photo;
                emp.DepartmentId = dto.DepartmentId;
                emp.DesignationId = dto.DesignationId;
                emp.BranchId = dto.BranchId;
                emp.IsActive = dto.IsActive;
                emp.ModifiedBy = _currentUser.UserId;
                emp.ModifiedAt = DateTime.Now;

                await _uow.Employees.UpdateAsync(emp);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync("Employees", "Update", _currentUser.UserId, "Employee", emp.Id);
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
            emp.IsDeleted = true;
            emp.ModifiedBy = deletedBy;
            emp.ModifiedAt = DateTime.Now;
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Employees", "Delete", deletedBy, "Employee", id);
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
        Id = e.Id, EmployeeCode = e.EmployeeCode, FirstName = e.FirstName,
        LastName = e.LastName, Email = e.Email, Phone = e.Phone,
        DateOfBirth = e.DateOfBirth, JoiningDate = e.JoiningDate, Gender = e.Gender,
        Address = e.Address, Photo = e.Photo, DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name ?? string.Empty, DesignationId = e.DesignationId,
        DesignationName = e.Designation?.Name ?? string.Empty, BranchId = e.BranchId,
        BranchName = e.Branch?.Name ?? string.Empty, IsActive = e.IsActive, CreatedAt = e.CreatedAt
    };

    private static EmployeeListItemDto MapToListDto(Employee e) => new()
    {
        Id = e.Id, EmployeeCode = e.EmployeeCode,
        FullName = $"{e.FirstName} {e.LastName}",
        Department = e.Department?.Name ?? string.Empty,
        Designation = e.Designation?.Name ?? string.Empty,
        Branch = e.Branch?.Name ?? string.Empty,
        Phone = e.Phone, Email = e.Email, IsActive = e.IsActive
    };
}

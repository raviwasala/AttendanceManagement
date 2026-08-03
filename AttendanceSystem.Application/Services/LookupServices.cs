using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>Department CRUD + search service.</summary>
public class DepartmentService : IDepartmentService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    public DepartmentService(IUnitOfWork uow, IAuditService audit) { _uow = uow; _audit = audit; }

    public async Task<Result<IEnumerable<DepartmentDto>>> GetAllAsync()
    {
        try
        {
            var list = await _uow.Departments.GetAllAsync();
            var employees = await _uow.Employees.GetAllAsync();
            var empCount = employees.GroupBy(e => e.DepartmentId)
                                    .ToDictionary(g => g.Key, g => g.Count());
            var dtos = list.OrderBy(d => d.Name).Select(d => new DepartmentDto
            {
                Id = d.Id, Name = d.Name, Description = d.Description,
                IsActive = d.IsActive,
                EmployeeCount = empCount.TryGetValue(d.Id, out var c) ? c : 0
            }).ToList();
            return Result<IEnumerable<DepartmentDto>>.Success(dtos);
        }
        catch (Exception ex) { AppLogger.Error("DepartmentService.GetAllAsync", ex); return Result<IEnumerable<DepartmentDto>>.Failure(ex.Message); }
    }

    public async Task<Result<DepartmentDto>> GetByIdAsync(int id)
    {
        try
        {
            var d = await _uow.Departments.GetByIdAsync(id);
            if (d == null) return Result<DepartmentDto>.Failure("Department not found.");
            return Result<DepartmentDto>.Success(Map(d));
        }
        catch (Exception ex) { AppLogger.Error("DepartmentService.GetByIdAsync", ex); return Result<DepartmentDto>.Failure(ex.Message); }
    }

    public async Task<Result<DepartmentDto>> SaveAsync(SaveDepartmentDto dto)
    {
        try
        {
            // Duplicate name check
            var all = await _uow.Departments.GetAllAsync();
            if (all.Any(d => d.Name.Equals(dto.Name.Trim(), StringComparison.OrdinalIgnoreCase) && d.Id != dto.Id))
                return Result<DepartmentDto>.Failure($"Department '{dto.Name}' already exists.");

            if (dto.Id == 0)
            {
                var entity = new Department { Name = dto.Name.Trim(), Description = dto.Description?.Trim(), IsActive = dto.IsActive, CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now };
                await _uow.Departments.AddAsync(entity);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync("Departments", "Create", AppSession.UserId, "Department", entity.Id);
                return Result<DepartmentDto>.Success(Map(entity));
            }
            else
            {
                var entity = await _uow.Departments.GetByIdAsync(dto.Id);
                if (entity == null) return Result<DepartmentDto>.Failure("Department not found.");
                entity.Name = dto.Name.Trim(); entity.Description = dto.Description?.Trim(); entity.IsActive = dto.IsActive;
                entity.ModifiedBy = AppSession.UserId; entity.ModifiedAt = DateTime.Now;
                await _uow.Departments.UpdateAsync(entity);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync("Departments", "Update", AppSession.UserId, "Department", entity.Id);
                return Result<DepartmentDto>.Success(Map(entity));
            }
        }
        catch (Exception ex) { AppLogger.Error("DepartmentService.SaveAsync", ex); return Result<DepartmentDto>.Failure(ex.Message); }
    }

    public async Task<Result> DeleteAsync(int id, int deletedBy)
    {
        try
        {
            var employees = await _uow.Employees.FindAsync(e => e.DepartmentId == id);
            if (employees.Any()) return Result.Failure("Cannot delete — employees are assigned to this department.");
            var entity = await _uow.Departments.GetByIdAsync(id);
            if (entity == null) return Result.Failure("Department not found.");
            entity.IsDeleted = true; entity.ModifiedBy = deletedBy; entity.ModifiedAt = DateTime.Now;
            await _uow.Departments.UpdateAsync(entity);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Departments", "Delete", deletedBy, "Department", id);
            return Result.Success();
        }
        catch (Exception ex) { AppLogger.Error("DepartmentService.DeleteAsync", ex); return Result.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<DepartmentDto>>> SearchAsync(string keyword)
    {
        try
        {
            var all = await _uow.Departments.GetAllAsync();
            var lower = keyword.ToLower();
            var filtered = all.Where(d => d.Name.ToLower().Contains(lower) || (d.Description ?? "").ToLower().Contains(lower));
            return Result<IEnumerable<DepartmentDto>>.Success(filtered.Select(Map));
        }
        catch (Exception ex) { AppLogger.Error("DepartmentService.SearchAsync", ex); return Result<IEnumerable<DepartmentDto>>.Failure(ex.Message); }
    }

    private static DepartmentDto Map(Department d) => new() { Id = d.Id, Name = d.Name, Description = d.Description, IsActive = d.IsActive };
}

/// <summary>Designation CRUD service.</summary>
public class DesignationService : IDesignationService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    public DesignationService(IUnitOfWork uow, IAuditService audit) { _uow = uow; _audit = audit; }

    public async Task<Result<IEnumerable<DesignationDto>>> GetAllAsync()
    {
        try
        {
            var list = await _uow.Designations.GetAllAsync();
            return Result<IEnumerable<DesignationDto>>.Success(list.OrderBy(d => d.Name).Select(Map));
        }
        catch (Exception ex) { return Result<IEnumerable<DesignationDto>>.Failure(ex.Message); }
    }

    public async Task<Result<DesignationDto>> GetByIdAsync(int id)
    {
        try
        {
            var d = await _uow.Designations.GetByIdAsync(id);
            return d == null ? Result<DesignationDto>.Failure("Not found.") : Result<DesignationDto>.Success(Map(d));
        }
        catch (Exception ex) { return Result<DesignationDto>.Failure(ex.Message); }
    }

    public async Task<Result<DesignationDto>> SaveAsync(SaveDesignationDto dto)
    {
        try
        {
            var all = await _uow.Designations.GetAllAsync();
            if (all.Any(d => d.Name.Equals(dto.Name.Trim(), StringComparison.OrdinalIgnoreCase) && d.Id != dto.Id))
                return Result<DesignationDto>.Failure($"Designation '{dto.Name}' already exists.");

            if (dto.Id == 0)
            {
                var entity = new Designation { Name = dto.Name.Trim(), Description = dto.Description?.Trim(), IsActive = dto.IsActive, CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now };
                await _uow.Designations.AddAsync(entity);
                await _uow.SaveChangesAsync();
                await _audit.LogAsync("Designations", "Create", AppSession.UserId, "Designation", entity.Id);
                return Result<DesignationDto>.Success(Map(entity));
            }
            else
            {
                var entity = await _uow.Designations.GetByIdAsync(dto.Id);
                if (entity == null) return Result<DesignationDto>.Failure("Not found.");
                entity.Name = dto.Name.Trim(); entity.Description = dto.Description?.Trim(); entity.IsActive = dto.IsActive;
                entity.ModifiedBy = AppSession.UserId; entity.ModifiedAt = DateTime.Now;
                await _uow.Designations.UpdateAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<DesignationDto>.Success(Map(entity));
            }
        }
        catch (Exception ex) { return Result<DesignationDto>.Failure(ex.Message); }
    }

    public async Task<Result> DeleteAsync(int id, int deletedBy)
    {
        try
        {
            var employees = await _uow.Employees.FindAsync(e => e.DesignationId == id);
            if (employees.Any()) return Result.Failure("Cannot delete — employees are assigned to this designation.");
            var entity = await _uow.Designations.GetByIdAsync(id);
            if (entity == null) return Result.Failure("Not found.");
            entity.IsDeleted = true; entity.ModifiedBy = deletedBy; entity.ModifiedAt = DateTime.Now;
            await _uow.Designations.UpdateAsync(entity);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    private static DesignationDto Map(Designation d) => new() { Id = d.Id, Name = d.Name, Description = d.Description, IsActive = d.IsActive };
}

/// <summary>Branch CRUD service.</summary>
public class BranchService : IBranchService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    public BranchService(IUnitOfWork uow, IAuditService audit) { _uow = uow; _audit = audit; }

    public async Task<Result<IEnumerable<BranchDto>>> GetAllAsync()
    {
        try
        {
            var list = await _uow.Branches.GetAllAsync();
            return Result<IEnumerable<BranchDto>>.Success(list.OrderBy(b => b.Name).Select(Map));
        }
        catch (Exception ex) { return Result<IEnumerable<BranchDto>>.Failure(ex.Message); }
    }

    public async Task<Result<BranchDto>> GetByIdAsync(int id)
    {
        try
        {
            var b = await _uow.Branches.GetByIdAsync(id);
            return b == null ? Result<BranchDto>.Failure("Not found.") : Result<BranchDto>.Success(Map(b));
        }
        catch (Exception ex) { return Result<BranchDto>.Failure(ex.Message); }
    }

    public async Task<Result<BranchDto>> SaveAsync(SaveBranchDto dto)
    {
        try
        {
            if (dto.Id == 0)
            {
                var entity = new Branch { Name = dto.Name.Trim(), Address = dto.Address?.Trim(), Phone = dto.Phone?.Trim(), IsActive = dto.IsActive, CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now };
                await _uow.Branches.AddAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<BranchDto>.Success(Map(entity));
            }
            else
            {
                var entity = await _uow.Branches.GetByIdAsync(dto.Id);
                if (entity == null) return Result<BranchDto>.Failure("Not found.");
                entity.Name = dto.Name.Trim(); entity.Address = dto.Address?.Trim(); entity.Phone = dto.Phone?.Trim(); entity.IsActive = dto.IsActive;
                entity.ModifiedBy = AppSession.UserId; entity.ModifiedAt = DateTime.Now;
                await _uow.Branches.UpdateAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<BranchDto>.Success(Map(entity));
            }
        }
        catch (Exception ex) { return Result<BranchDto>.Failure(ex.Message); }
    }

    public async Task<Result> DeleteAsync(int id, int deletedBy)
    {
        try
        {
            var employees = await _uow.Employees.FindAsync(e => e.BranchId == id);
            if (employees.Any()) return Result.Failure("Cannot delete — employees are assigned to this branch.");
            var entity = await _uow.Branches.GetByIdAsync(id);
            if (entity == null) return Result.Failure("Not found.");
            entity.IsDeleted = true; entity.ModifiedBy = deletedBy; entity.ModifiedAt = DateTime.Now;
            await _uow.Branches.UpdateAsync(entity);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    private static BranchDto Map(Branch b) => new() { Id = b.Id, Name = b.Name, Address = b.Address, Phone = b.Phone, IsActive = b.IsActive };
}

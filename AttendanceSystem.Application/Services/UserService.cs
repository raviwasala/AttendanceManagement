using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Helpers;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>User and role/permission management service.</summary>
public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    public UserService(IUnitOfWork uow, IAuditService audit) { _uow = uow; _audit = audit; }

    public async Task<Result<IEnumerable<UserDto>>> GetAllAsync()
    {
        try
        {
            var all = await _uow.Users.GetAllAsync();
            return Result<IEnumerable<UserDto>>.Success(all.Select(Map));
        }
        catch (Exception ex) { return Result<IEnumerable<UserDto>>.Failure(ex.Message); }
    }

    public async Task<Result<UserDto>> GetByIdAsync(int id)
    {
        try
        {
            var u = await _uow.Users.GetByIdAsync(id);
            return u == null ? Result<UserDto>.Failure("User not found.") : Result<UserDto>.Success(Map(u));
        }
        catch (Exception ex) { return Result<UserDto>.Failure(ex.Message); }
    }

    public async Task<Result<UserDto>> CreateAsync(CreateUserDto dto)
    {
        try
        {
            if (await _uow.Users.IsUsernameTakenAsync(dto.Username))
                return Result<UserDto>.Failure($"Username '{dto.Username}' is already taken.");

            var (isValid, msg) = PasswordHelper.ValidateStrength(dto.Password);
            if (!isValid) return Result<UserDto>.Failure(msg);

            var user = new User
            {
                Username = dto.Username.Trim().ToLower(),
                Email = dto.Email.Trim(),
                FullName = dto.FullName.Trim(),
                PasswordHash = PasswordHelper.HashPassword(dto.Password),
                RoleId = dto.RoleId,
                EmployeeId = dto.EmployeeId,
                IsActive = dto.IsActive,
                CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now
            };
            await _uow.Users.AddAsync(user);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Users", "Create", AppSession.UserId, "User", user.Id);
            return Result<UserDto>.Success(Map(user));
        }
        catch (Exception ex) { AppLogger.Error("UserService.CreateAsync", ex); return Result<UserDto>.Failure(ex.Message); }
    }

    public async Task<Result> UpdateAsync(UpdateUserDto dto)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(dto.Id);
            if (user == null) return Result.Failure("User not found.");
            user.Email = dto.Email.Trim(); user.FullName = dto.FullName.Trim();
            user.RoleId = dto.RoleId; user.EmployeeId = dto.EmployeeId; user.IsActive = dto.IsActive;
            user.ModifiedBy = AppSession.UserId; user.ModifiedAt = DateTime.Now;
            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Users", "Update", AppSession.UserId, "User", dto.Id);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result> DeleteAsync(int id, int deletedBy)
    {
        try
        {
            if (id == 1) return Result.Failure("Cannot delete the default administrator account.");
            var user = await _uow.Users.GetByIdAsync(id);
            if (user == null) return Result.Failure("User not found.");
            user.IsDeleted = true; user.ModifiedBy = deletedBy; user.ModifiedAt = DateTime.Now;
            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result> ResetPasswordAsync(int userId, string newPassword, int resetBy)
    {
        try
        {
            var (isValid, msg) = PasswordHelper.ValidateStrength(newPassword);
            if (!isValid) return Result.Failure(msg);
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null) return Result.Failure("User not found.");
            user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            user.PasswordChangedAt = DateTime.Now;
            user.ModifiedBy = resetBy; user.ModifiedAt = DateTime.Now;
            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();
            await _audit.LogAsync("Users", "ResetPassword", resetBy, "User", userId);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result> LockAsync(int userId)
    {
        try { await _uow.Users.LockUserAsync(userId); await _uow.SaveChangesAsync(); return Result.Success(); }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result> UnlockAsync(int userId)
    {
        try { await _uow.Users.UnlockUserAsync(userId); await _uow.SaveChangesAsync(); return Result.Success(); }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    private static UserDto Map(User u) => new()
    {
        Id = u.Id, Username = u.Username, Email = u.Email, FullName = u.FullName,
        RoleId = u.RoleId, RoleName = u.Role?.Name ?? string.Empty,
        EmployeeId = u.EmployeeId, IsActive = u.IsActive, IsLocked = u.IsLocked,
        LastLoginAt = u.LastLoginAt, CreatedAt = u.CreatedAt
    };
}

/// <summary>Role and permission management service.</summary>
public class RoleService : IRoleService
{
    private readonly IUnitOfWork _uow;
    public RoleService(IUnitOfWork uow) { _uow = uow; }

    public async Task<Result<IEnumerable<RoleDto>>> GetAllAsync()
    {
        try
        {
            var list = await _uow.Roles.GetAllAsync();
            return Result<IEnumerable<RoleDto>>.Success(list.Select(r => new RoleDto { Id = r.Id, Name = r.Name, Description = r.Description }));
        }
        catch (Exception ex) { return Result<IEnumerable<RoleDto>>.Failure(ex.Message); }
    }

    public async Task<Result<RoleDto>> GetByIdAsync(int id)
    {
        try
        {
            var r = await _uow.Roles.GetByIdAsync(id);
            return r == null ? Result<RoleDto>.Failure("Not found.") : Result<RoleDto>.Success(new RoleDto { Id = r.Id, Name = r.Name, Description = r.Description });
        }
        catch (Exception ex) { return Result<RoleDto>.Failure(ex.Message); }
    }

    public async Task<Result<RoleDto>> SaveAsync(RoleDto dto)
    {
        try
        {
            if (dto.Id == 0)
            {
                var entity = new Role { Name = dto.Name.Trim(), Description = dto.Description?.Trim(), CreatedBy = AppSession.UserId, CreatedAt = DateTime.Now };
                await _uow.Roles.AddAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<RoleDto>.Success(new RoleDto { Id = entity.Id, Name = entity.Name });
            }
            else
            {
                var entity = await _uow.Roles.GetByIdAsync(dto.Id);
                if (entity == null) return Result<RoleDto>.Failure("Not found.");
                entity.Name = dto.Name.Trim(); entity.Description = dto.Description?.Trim();
                entity.ModifiedBy = AppSession.UserId; entity.ModifiedAt = DateTime.Now;
                await _uow.Roles.UpdateAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<RoleDto>.Success(new RoleDto { Id = entity.Id, Name = entity.Name });
            }
        }
        catch (Exception ex) { return Result<RoleDto>.Failure(ex.Message); }
    }

    public async Task<Result> DeleteAsync(int id)
    {
        try
        {
            var usersInRole = await _uow.Users.FindAsync(u => u.RoleId == id);
            if (usersInRole.Any()) return Result.Failure("Cannot delete — users are assigned to this role.");
            var entity = await _uow.Roles.GetByIdAsync(id);
            if (entity == null) return Result.Failure("Not found.");
            entity.IsDeleted = true; entity.ModifiedBy = AppSession.UserId; entity.ModifiedAt = DateTime.Now;
            await _uow.Roles.UpdateAsync(entity);
            await _uow.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }

    public async Task<Result<IEnumerable<PermissionDto>>> GetPermissionsForRoleAsync(int roleId)
    {
        try
        {
            var allPerms = await _uow.Permissions.GetAllAsync();
            var rolePerms = await _uow.RolePermissions.FindAsync(rp => rp.RoleId == roleId);
            var grantedIds = rolePerms.Select(rp => rp.PermissionId).ToHashSet();
            var dtos = allPerms.Select(p => new PermissionDto
            {
                Id = p.Id, Module = p.Module, Action = p.Action,
                DisplayName = p.DisplayName, IsGranted = grantedIds.Contains(p.Id)
            });
            return Result<IEnumerable<PermissionDto>>.Success(dtos);
        }
        catch (Exception ex) { return Result<IEnumerable<PermissionDto>>.Failure(ex.Message); }
    }

    public async Task<Result> SavePermissionsAsync(int roleId, IEnumerable<int> permissionIds)
    {
        try
        {
            await _uow.SavePermissionsAsync(roleId, permissionIds);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.Message); }
    }
}

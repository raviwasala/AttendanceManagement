using System.Text.Json;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Constants;
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
    private readonly ICurrentUserContext _currentUser;
    public UserService(IUnitOfWork uow, IAuditService audit, ICurrentUserContext currentUser) { _uow = uow; _audit = audit; _currentUser = currentUser; }

    public async Task<Result<IEnumerable<UserDto>>> GetAllAsync()
    {
        try
        {
            var all = await _uow.Users.GetAllAsync();
            return Result<IEnumerable<UserDto>>.Success(all.Select(Map).ToList());
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

            var linkError = await ValidateEmployeeLinkAsync(dto.RoleId, dto.EmployeeId);
            if (linkError != null) return Result<UserDto>.Failure(linkError);

            var user = new User
            {
                Username = dto.Username.Trim().ToLower(),
                Email = dto.Email.Trim(),
                FullName = dto.FullName.Trim(),
                PasswordHash = PasswordHelper.HashPassword(dto.Password),
                RoleId = dto.RoleId,
                EmployeeId = dto.EmployeeId,
                IsActive = dto.IsActive, ApprovalScope = dto.ApprovalScope,
                CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now
            };
            await _uow.Users.AddAsync(user);
            await _uow.SaveChangesAsync();
            // AuditSnapshot drops anything whose name looks like a secret, so PasswordHash and
            // the remember-me/reset token hashes never reach the audit table.
            await _audit.LogAsync("Users", "Create", _currentUser.UserId, "User", user.Id,
                newValues: AuditSnapshot.Snapshot(user));
            return Result<UserDto>.Success(Map(user));
        }
        catch (Exception ex) { AppLogger.Error("UserService.CreateAsync", ex); return Result<UserDto>.Failure(ex.Message); }
    }

    /// <summary>
    /// Rejects an approver with no employee record behind them; returns null when the pairing
    /// is fine.
    ///
    /// Linking a user to an employee is optional in general — a pure administrator or a service
    /// login has no employee record and should not need a fake one. But approval leans on that
    /// link twice: blocking self-approval needs to know which employee this user *is*, and
    /// department-head scope is recorded against the employee, not the user. An approver
    /// without it silently gets neither check, which is the failure that matters — they would
    /// be able to sign off their own request.
    /// </summary>
    private async Task<string?> ValidateEmployeeLinkAsync(int roleId, int? employeeId)
    {
        if (employeeId.HasValue) return null;

        var roles = await GetRolesCanApproveAsync();
        if (!roles.Contains(roleId)) return null;

        return "This role can approve leave or overtime, so the user must be linked to an " +
               "employee. Without it the system cannot tell whose requests are their own, " +
               "and they could approve their own.";
    }

    /// <summary>
    /// Ids of roles holding any <c>*.Approve</c> permission. Role 1 is Administrator, which
    /// holds every permission implicitly rather than through RolePermissions rows.
    /// </summary>
    private async Task<HashSet<int>> GetRolesCanApproveAsync()
    {
        var approveIds = (await _uow.Permissions.FindAsync(p => p.Action == AppConstants.Actions.Approve))
            .Select(p => p.Id).ToHashSet();

        var ids = new HashSet<int> { 1 };
        foreach (var r in await _uow.Roles.GetAllAsync())
        {
            if ((await _uow.GetRolePermissionsAsync(r.Id)).Any(rp => approveIds.Contains(rp.PermissionId)))
                ids.Add(r.Id);
        }
        return ids;
    }

    public async Task<Result> UpdateAsync(UpdateUserDto dto)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(dto.Id);
            if (user == null) return Result.Failure("User not found.");

            var linkError = await ValidateEmployeeLinkAsync(dto.RoleId, dto.EmployeeId);
            if (linkError != null) return Result.Failure(linkError);

            // RoleId changes are the interesting ones here — this is how somebody gains rights.
            var before = AuditSnapshot.Capture(user);

            user.Email = dto.Email.Trim(); user.FullName = dto.FullName.Trim();
            user.RoleId = dto.RoleId; user.EmployeeId = dto.EmployeeId; user.IsActive = dto.IsActive; user.ApprovalScope = dto.ApprovalScope;
            user.ModifiedBy = _currentUser.UserId; user.ModifiedAt = DateTime.Now;
            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();

            var (oldValues, newValues) = AuditSnapshot.DiffAgainst(before, user);
            await _audit.LogAsync("Users", "Update", _currentUser.UserId, "User", dto.Id,
                oldValues, newValues);
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
        EmployeeId = u.EmployeeId,
        EmployeeName = u.Employee != null ? $"{u.Employee.FirstName} {u.Employee.LastName}".Trim() : null,
        IsActive = u.IsActive, IsLocked = u.IsLocked, ApprovalScope = u.ApprovalScope,
        LastLoginAt = u.LastLoginAt, CreatedAt = u.CreatedAt
    };
}

/// <summary>Role and permission management service.</summary>
public class RoleService : IRoleService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditService _audit;

    public RoleService(IUnitOfWork uow, ICurrentUserContext currentUser, IAuditService audit)
    {
        _uow = uow;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Result<IEnumerable<RoleDto>>> GetAllAsync()
    {
        try
        {
            var list = (await _uow.Roles.GetAllAsync()).ToList();

            // Which roles can approve anything at all. Roles are a handful of rows, so the
            // per-role lookup costs nothing; the alternative is exposing RolePermissions to
            // the Users screen, which would leak far more than the one bit it needs.
            var approveIds = (await _uow.Permissions.FindAsync(p => p.Action == AppConstants.Actions.Approve))
                .Select(p => p.Id).ToHashSet();

            var dtos = new List<RoleDto>();
            foreach (var r in list)
            {
                // Role 1 is Administrator, which holds every permission implicitly rather than
                // through rows in RolePermissions — the same rule GetPermissionsAsync applies.
                var canApprove = r.Id == 1 ||
                    (await _uow.GetRolePermissionsAsync(r.Id)).Any(rp => approveIds.Contains(rp.PermissionId));

                dtos.Add(new RoleDto
                {
                    Id = r.Id, Name = r.Name, Description = r.Description, CanApprove = canApprove
                });
            }

            return Result<IEnumerable<RoleDto>>.Success(dtos);
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
                var entity = new Role { Name = dto.Name.Trim(), Description = dto.Description?.Trim(), CreatedBy = _currentUser.UserId, CreatedAt = DateTime.Now };
                await _uow.Roles.AddAsync(entity);
                await _uow.SaveChangesAsync();
                return Result<RoleDto>.Success(new RoleDto { Id = entity.Id, Name = entity.Name });
            }
            else
            {
                var entity = await _uow.Roles.GetByIdAsync(dto.Id);
                if (entity == null) return Result<RoleDto>.Failure("Not found.");
                entity.Name = dto.Name.Trim(); entity.Description = dto.Description?.Trim();
                entity.ModifiedBy = _currentUser.UserId; entity.ModifiedAt = DateTime.Now;
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
            entity.IsDeleted = true; entity.ModifiedBy = _currentUser.UserId; entity.ModifiedAt = DateTime.Now;
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
            var allPerms = (await _uow.Permissions.GetAllAsync()).ToList();
            if (!allPerms.Any())
            {
                // Auto-seed system permissions
                allPerms = GetSystemSeedPermissions();
                foreach (var p in allPerms)
                {
                    await _uow.Permissions.AddAsync(p);
                }
                await _uow.SaveChangesAsync();
                allPerms = (await _uow.Permissions.GetAllAsync()).ToList();
            }

            var rolePerms = await _uow.GetRolePermissionsAsync(roleId);
            var grantedIds = rolePerms.Select(rp => rp.PermissionId).ToHashSet();
            
            // Administrator role gets all permissions granted by default
            var isAdmin = roleId == 1;

            var dtos = allPerms.Select(p => new PermissionDto
            {
                Id = p.Id,
                Module = p.Module,
                Action = p.Action,
                DisplayName = p.DisplayName,
                IsGranted = isAdmin || grantedIds.Contains(p.Id)
            });
            return Result<IEnumerable<PermissionDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            AppLogger.Error("RoleService.GetPermissionsForRoleAsync", ex);
            return Result<IEnumerable<PermissionDto>>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Replaces a role's permissions.
    ///
    /// Audited as granted/revoked permission <em>keys</em> rather than ids: "Overtime.Approve"
    /// answers the question an auditor is actually asking, where "61" needs a lookup against a
    /// table that may have been renumbered since. Nothing about this operation was recorded
    /// before — granting rights was the least traceable action in the system.
    /// </summary>
    public async Task<Result> SavePermissionsAsync(int roleId, IEnumerable<int> permissionIds)
    {
        try
        {
            var requested = permissionIds?.Distinct().ToHashSet() ?? [];

            var allPermissions = (await _uow.Permissions.GetAllAsync())
                .ToDictionary(p => p.Id, p => $"{p.Module}.{p.Action}");
            var currentIds = (await _uow.GetRolePermissionsAsync(roleId))
                .Select(rp => rp.PermissionId).ToHashSet();

            var granted = requested.Except(currentIds)
                .Select(id => allPermissions.TryGetValue(id, out var k) ? k : $"#{id}")
                .OrderBy(k => k).ToList();
            var revoked = currentIds.Except(requested)
                .Select(id => allPermissions.TryGetValue(id, out var k) ? k : $"#{id}")
                .OrderBy(k => k).ToList();

            await _uow.SavePermissionsAsync(roleId, requested);
            await _uow.SaveChangesAsync();

            // A save that changed nothing is not worth an entry.
            if (granted.Count > 0 || revoked.Count > 0)
            {
                var role = await _uow.Roles.GetByIdAsync(roleId);
                await _audit.LogAsync(AppConstants.Modules.Roles, "UpdatePermissions",
                    _currentUser.UserId, nameof(Role), roleId,
                    oldValues: revoked.Count > 0
                        ? JsonSerializer.Serialize(new { Role = role?.Name, Revoked = revoked })
                        : null,
                    newValues: granted.Count > 0
                        ? JsonSerializer.Serialize(new { Role = role?.Name, Granted = granted })
                        : null);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            AppLogger.Error("RoleService.SavePermissionsAsync", ex);
            return Result.Failure(ex.Message);
        }
    }

    private static List<Permission> GetSystemSeedPermissions()
    {
        var modules = new[]
        {
            "Departments", "Designations", "Branches", "Shifts", "Employees",
            "Attendance Records", "Leave Management", "Holidays", "Biometric Import",
            "System Reports", "System Users", "Role Access Control", "System Settings"
        };
        var actions = new[] { "View", "Add", "Edit", "Delete", "Print" };

        var list = new List<Permission>();
        foreach (var m in modules)
        {
            foreach (var a in actions)
            {
                var label = a == "View" ? $"View {m} Page" : $"{a} {m}";
                list.Add(new Permission
                {
                    Module = m,
                    Action = a,
                    DisplayName = label,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        return list;
    }
}

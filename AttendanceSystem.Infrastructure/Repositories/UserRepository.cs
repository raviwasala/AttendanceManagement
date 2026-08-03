using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>User-specific repository implementation.</summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AttendanceDbContext context) : base(context) { }

    public async Task<User?> GetByUsernameAsync(string username) =>
        await _dbSet.Include(u => u.Role).ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                    .FirstOrDefaultAsync(u => u.Username == username);

    public async Task<User?> GetByEmailAsync(string email) =>
        await _dbSet.Include(u => u.Role).Include(u => u.Employee).FirstOrDefaultAsync(u => u.Email == email);

    public override async Task<IEnumerable<User>> GetAllAsync(int? page = null, int? pageSize = null)
    {
        var query = _dbSet.AsNoTracking()
                          .Include(u => u.Role)
                          .Include(u => u.Employee)
                          .AsQueryable();
        if (page.HasValue && pageSize.HasValue)
            query = query.OrderBy(e => e.Id).Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value);
        return await query.ToListAsync();
    }

    public override async Task<User?> GetByIdAsync(int id)
    {
        return await _dbSet.Include(u => u.Role)
                           .Include(u => u.Employee)
                           .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> IsUsernameTakenAsync(string username, int? excludeId = null) =>
        await _dbSet.AnyAsync(u => u.Username == username && (!excludeId.HasValue || u.Id != excludeId.Value));

    public async Task IncrementFailedLoginAsync(int userId)
    {
        var user = await _dbSet.FindAsync(userId);
        if (user != null) { user.FailedLoginAttempts++; }
    }

    public async Task ResetFailedLoginAsync(int userId)
    {
        var user = await _dbSet.FindAsync(userId);
        if (user != null) { user.FailedLoginAttempts = 0; }
    }

    public async Task LockUserAsync(int userId)
    {
        var user = await _dbSet.FindAsync(userId);
        if (user != null) { user.IsLocked = true; }
    }

    public async Task UnlockUserAsync(int userId)
    {
        var user = await _dbSet.FindAsync(userId);
        if (user != null) { user.IsLocked = false; user.FailedLoginAttempts = 0; }
    }
}

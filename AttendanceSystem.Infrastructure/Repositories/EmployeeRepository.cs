using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>Employee-specific repository implementation.</summary>
public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(AttendanceDbContext context) : base(context) { }

    public async Task<Employee?> GetWithDetailsAsync(int id) =>
        await _dbSet.AsNoTracking()
                    .Include(e => e.Department)
                    .Include(e => e.Designation)
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<IEnumerable<Employee>> GetActiveEmployeesAsync() =>
        await _dbSet.AsNoTracking()
                    .Include(e => e.Department)
                    .Include(e => e.Designation)
                    .Include(e => e.Branch)
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.FirstName)
                    .ToListAsync();

    public async Task<bool> IsCodeTakenAsync(string code, int? excludeId = null) =>
        await _dbSet.AnyAsync(e => e.EmployeeCode == code && (!excludeId.HasValue || e.Id != excludeId.Value));

    public async Task<string> GenerateNextCodeAsync()
    {
        var last = await _dbSet.IgnoreQueryFilters()
                               .OrderByDescending(e => e.Id)
                               .FirstOrDefaultAsync();
        var next = (last?.Id ?? 0) + 1;
        return $"EMP-{next:D5}";
    }

    /// <summary>
    /// Uses EF.Functions.Like for index-friendly, case-insensitive search
    /// (relies on database collation — avoids client-side LOWER() that bypasses indexes).
    /// </summary>
    public async Task<IEnumerable<Employee>> SearchAsync(string keyword)
    {
        var pattern = $"%{keyword}%";
        return await _dbSet.AsNoTracking()
                           .Include(e => e.Department)
                           .Include(e => e.Designation)
                           .Include(e => e.Branch)
                           .Where(e => EF.Functions.Like(e.FirstName, pattern)
                                    || EF.Functions.Like(e.LastName, pattern)
                                    || EF.Functions.Like(e.EmployeeCode, pattern)
                                    || (e.Email != null && EF.Functions.Like(e.Email, pattern))
                                    || (e.Phone != null && EF.Functions.Like(e.Phone, pattern)))
                           .OrderBy(e => e.FirstName)
                           .ToListAsync();
    }
}

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
        await _dbSet.Include(e => e.Department)
                    .Include(e => e.Designation)
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<IEnumerable<Employee>> GetActiveEmployeesAsync() =>
        await _dbSet.Include(e => e.Department)
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

    public async Task<IEnumerable<Employee>> SearchAsync(string keyword)
    {
        var lower = keyword.ToLower();
        return await _dbSet.Include(e => e.Department)
                           .Include(e => e.Designation)
                           .Include(e => e.Branch)
                           .Where(e => e.FirstName.ToLower().Contains(lower)
                                    || e.LastName.ToLower().Contains(lower)
                                    || e.EmployeeCode.ToLower().Contains(lower)
                                    || (e.Email != null && e.Email.ToLower().Contains(lower))
                                    || (e.Phone != null && e.Phone.Contains(lower)))
                           .OrderBy(e => e.FirstName)
                           .ToListAsync();
    }
}

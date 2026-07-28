using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>Holiday-specific repository implementation.</summary>
public class HolidayRepository : Repository<Holiday>, IHolidayRepository
{
    public HolidayRepository(AttendanceDbContext context) : base(context) { }

    public async Task<bool> IsHolidayAsync(DateTime date) =>
        await _dbSet.AnyAsync(h => h.HolidayDate.Date == date.Date);

    public async Task<IEnumerable<Holiday>> GetByYearAsync(int year) =>
        await _dbSet.Where(h => h.HolidayDate.Year == year)
                    .OrderBy(h => h.HolidayDate)
                    .ToListAsync();
}

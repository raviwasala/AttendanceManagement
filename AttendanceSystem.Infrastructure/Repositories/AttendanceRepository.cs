using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>Attendance-specific repository implementation.</summary>
public class AttendanceRepository : Repository<AttendanceLog>, IAttendanceRepository
{
    public AttendanceRepository(AttendanceDbContext context) : base(context) { }

    public async Task<AttendanceLog?> GetTodayAttendanceAsync(int employeeId, DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        return await _dbSet.AsNoTracking()
                           .FirstOrDefaultAsync(a => a.EmployeeId == employeeId
                                                  && a.AttendanceDate >= start
                                                  && a.AttendanceDate < end);
    }

    public async Task<IEnumerable<AttendanceLog>> GetByEmployeeAndDateRangeAsync(
        int employeeId, DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);
        return await _dbSet.AsNoTracking()
                           .Include(a => a.Employee)
                           .Where(a => a.EmployeeId == employeeId
                                    && a.AttendanceDate >= start
                                    && a.AttendanceDate < end)
                           .OrderByDescending(a => a.AttendanceDate)
                           .ToListAsync();
    }

    public async Task<IEnumerable<AttendanceLog>> GetByDateAsync(DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        return await _dbSet.AsNoTracking()
                           .Include(a => a.Employee)
                           .ThenInclude(e => e.Department)
                           .Where(a => a.AttendanceDate >= start && a.AttendanceDate < end)
                           .OrderBy(a => a.Employee.FirstName)
                           .ToListAsync();
    }

    public async Task<int> GetPresentCountTodayAsync(DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        return await _dbSet.CountAsync(a => a.AttendanceDate >= start
                                         && a.AttendanceDate < end
                                         && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late));
    }

    /// <summary>Inlined present count to avoid a second DB round-trip.</summary>
    public async Task<int> GetAbsentCountTodayAsync(DateTime date, int totalEmployees)
    {
        var present = await GetPresentCountTodayAsync(date);
        return Math.Max(0, totalEmployees - present);
    }

    public async Task<int> GetLateCountTodayAsync(DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        return await _dbSet.CountAsync(a => a.AttendanceDate >= start && a.AttendanceDate < end && a.IsLate);
    }
}

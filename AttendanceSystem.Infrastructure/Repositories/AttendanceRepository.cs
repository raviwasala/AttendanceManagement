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

    public async Task<AttendanceLog?> GetTodayAttendanceAsync(int employeeId, DateTime date) =>
        await _dbSet.FirstOrDefaultAsync(a => a.EmployeeId == employeeId
                                            && a.AttendanceDate.Date == date.Date);

    public async Task<IEnumerable<AttendanceLog>> GetByEmployeeAndDateRangeAsync(
        int employeeId, DateTime from, DateTime to) =>
        await _dbSet.Include(a => a.Employee)
                    .Where(a => a.EmployeeId == employeeId
                             && a.AttendanceDate.Date >= from.Date
                             && a.AttendanceDate.Date <= to.Date)
                    .OrderByDescending(a => a.AttendanceDate)
                    .ToListAsync();

    public async Task<IEnumerable<AttendanceLog>> GetByDateAsync(DateTime date) =>
        await _dbSet.Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                    .Where(a => a.AttendanceDate.Date == date.Date)
                    .OrderBy(a => a.Employee.FirstName)
                    .ToListAsync();

    public async Task<int> GetPresentCountTodayAsync(DateTime date) =>
        await _dbSet.CountAsync(a => a.AttendanceDate.Date == date.Date
                                  && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late));

    /// <summary>Inlined present count to avoid a second DB round-trip.</summary>
    public async Task<int> GetAbsentCountTodayAsync(DateTime date, int totalEmployees)
    {
        var present = await _dbSet.CountAsync(a =>
            a.AttendanceDate.Date == date.Date &&
            (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late));
        return Math.Max(0, totalEmployees - present);
    }

    public async Task<int> GetLateCountTodayAsync(DateTime date) =>
        await _dbSet.CountAsync(a => a.AttendanceDate.Date == date.Date && a.IsLate);
}

using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>Leave-specific repository implementation.</summary>
public class LeaveRepository : Repository<LeaveRequest>, ILeaveRepository
{
    public LeaveRepository(AttendanceDbContext context) : base(context) { }

    public async Task<IEnumerable<LeaveRequest>> GetByEmployeeAsync(int employeeId) =>
        await _dbSet.Include(l => l.LeaveType)
                    .Include(l => l.Employee)
                    .Where(l => l.EmployeeId == employeeId)
                    .OrderByDescending(l => l.FromDate)
                    .ToListAsync();

    public async Task<IEnumerable<LeaveRequest>> GetPendingAsync() =>
        await _dbSet.Include(l => l.LeaveType)
                    .Include(l => l.Employee).ThenInclude(e => e.Department)
                    .Where(l => l.Status == LeaveStatus.Pending)
                    .OrderBy(l => l.FromDate)
                    .ToListAsync();

    public async Task<int> GetUsedLeaveDaysAsync(int employeeId, int leaveTypeId, int year) =>
        await _dbSet.Where(l => l.EmployeeId == employeeId
                             && l.LeaveTypeId == leaveTypeId
                             && l.FromDate.Year == year
                             && l.Status == LeaveStatus.Approved)
                    .SumAsync(l => l.TotalDays);
}

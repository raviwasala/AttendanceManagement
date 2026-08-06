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

    public async Task<(IEnumerable<LeaveRequest> Items, int TotalCount)> GetPagedAsync(
        string? search, LeaveStatus? status, int? departmentId, int? employeeId,
        DateTime? from, DateTime? to, int skip, int take,
        IReadOnlyCollection<int>? scopeDepartmentIds = null, int? scopeEmployeeId = null)
    {
        var query = _dbSet.AsNoTracking()
            .Include(l => l.LeaveType)
            .Include(l => l.Employee).ThenInclude(e => e.Department)
            .AsQueryable();

        // Visibility first, before paging, so the total count matches what is shown.
        if (scopeEmployeeId.HasValue)
            query = query.Where(l => l.EmployeeId == scopeEmployeeId.Value);
        else if (scopeDepartmentIds is { Count: > 0 })
            query = query.Where(l => scopeDepartmentIds.Contains(l.Employee.DepartmentId));

        if (status.HasValue) query = query.Where(l => l.Status == status.Value);
        if (employeeId.HasValue) query = query.Where(l => l.EmployeeId == employeeId.Value);
        if (departmentId.HasValue) query = query.Where(l => l.Employee.DepartmentId == departmentId.Value);

        // Overlap, not containment: a request spanning the window should appear even when
        // neither of its own dates falls inside it.
        if (from.HasValue) query = query.Where(l => l.ToDate >= from.Value);
        if (to.HasValue) query = query.Where(l => l.FromDate <= to.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                EF.Functions.Like(l.Employee.EmployeeCode, $"%{term}%") ||
                EF.Functions.Like(l.Employee.FirstName, $"%{term}%") ||
                EF.Functions.Like(l.Employee.LastName, $"%{term}%") ||
                EF.Functions.Like(l.Employee.FirstName + " " + l.Employee.LastName, $"%{term}%") ||
                EF.Functions.Like(l.LeaveType.Name, $"%{term}%") ||
                EF.Functions.Like(l.Reason, $"%{term}%"));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.FromDate).ThenByDescending(l => l.Id)
            .Skip(skip)
            .Take(take > 0 ? take : int.MaxValue)
            .ToListAsync();

        return (items, total);
    }

    public async Task<int> GetUsedLeaveDaysAsync(int employeeId, int leaveTypeId, int year) =>
        await _dbSet.Where(l => l.EmployeeId == employeeId
                             && l.LeaveTypeId == leaveTypeId
                             && l.FromDate.Year == year
                             && l.Status == LeaveStatus.Approved)
                    .SumAsync(l => l.TotalDays);

    public async Task<int> GetCommittedLeaveDaysAsync(int employeeId, int leaveTypeId, int year,
                                                      int? excludeRequestId = null) =>
        await _dbSet.Where(l => l.EmployeeId == employeeId
                             && l.LeaveTypeId == leaveTypeId
                             && l.FromDate.Year == year
                             && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending)
                             && (excludeRequestId == null || l.Id != excludeRequestId))
                    .SumAsync(l => l.TotalDays);

    public async Task<IEnumerable<LeaveRequest>> GetOverlappingAsync(
        int employeeId, DateTime from, DateTime to, int? excludeRequestId = null) =>
        // Standard overlap test: two ranges intersect when each starts on or before the
        // other ends. Cancelled and rejected requests hold no dates and are excluded.
        await _dbSet.Include(l => l.LeaveType)
                    .Where(l => l.EmployeeId == employeeId
                             && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending)
                             && l.FromDate.Date <= to.Date
                             && l.ToDate.Date >= from.Date
                             && (excludeRequestId == null || l.Id != excludeRequestId))
                    .ToListAsync();
}

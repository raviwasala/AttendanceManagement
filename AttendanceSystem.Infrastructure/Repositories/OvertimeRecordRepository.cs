using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>
/// Overtime claims. Exists on top of the generic repository so the register can fetch one page
/// of rows and the totals for the whole range without loading either twice.
/// </summary>
public class OvertimeRecordRepository : Repository<OvertimeRecord>, IOvertimeRecordRepository
{
    public OvertimeRecordRepository(AttendanceDbContext context) : base(context) { }

    public async Task<(IEnumerable<OvertimeRecord> Items, OvertimeTotals Totals)> GetRegisterPageAsync(
        DateTime from, DateTime to, int? employeeId, IReadOnlyCollection<int>? employeeIds,
        OvertimeStatus? status, int skip, int take)
    {
        var query = _dbSet.AsNoTracking()
            .Where(r => r.OvertimeDate >= from && r.OvertimeDate <= to);

        if (employeeId.HasValue) query = query.Where(r => r.EmployeeId == employeeId.Value);

        // A department filter arrives already resolved to employee ids. An empty collection is
        // a real answer — "that department has nobody" — and must return nothing, not everything.
        if (employeeIds != null) query = query.Where(r => employeeIds.Contains(r.EmployeeId));

        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        // Aggregated in SQL: one round trip, no rows materialised. Sums are over the whole
        // filtered range so the header tiles keep describing the range, not the current page.
        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new OvertimeTotals(
                g.Count(),
                g.Count(r => r.Status == OvertimeStatus.Pending),
                g.Count(r => r.Status == OvertimeStatus.Approved),
                g.Count(r => r.Status == OvertimeStatus.Rejected),
                g.Sum(r => r.ClaimedMinutes),
                g.Sum(r => r.ApprovedMinutes ?? 0),
                g.Sum(r => (r.ApprovedMinutes ?? 0) / 60m * r.RateMultiplier),
                // Each row weighted by its own rate — a single blended multiplier would be
                // wrong the moment holiday and ordinary overtime appear in the same range.
                g.Sum(r => r.ClaimedMinutes / 60m * r.RateMultiplier)))
            .FirstOrDefaultAsync()
            ?? new OvertimeTotals(0, 0, 0, 0, 0, 0, 0m, 0m);

        var items = await query
            .OrderBy(r => r.OvertimeDate).ThenBy(r => r.EmployeeId).ThenBy(r => r.Id)
            .Skip(skip)
            .Take(take > 0 ? take : int.MaxValue)
            .ToListAsync();

        return (items, totals);
    }
}

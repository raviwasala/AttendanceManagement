using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>
/// Audit log repository implementation.
/// AuditLog is an append-only entity that does not extend BaseEntity,
/// so this class does not inherit from the generic Repository&lt;T&gt;.
/// IMPORTANT: Do NOT call SaveChangesAsync here — let the Unit of Work manage the transaction boundary.
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly AttendanceDbContext _context;
    public AuditLogRepository(AttendanceDbContext context) => _context = context;

    /// <summary>Stages the audit log entry. The caller (Unit of Work) is responsible for committing.</summary>
    public async Task AddAsync(AuditLog log)
    {
        await _context.AuditLogs.AddAsync(log);
        // ✅ No SaveChangesAsync — let UoW.SaveChangesAsync() commit atomically
    }

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int count = 100) =>
        await _context.AuditLogs.Include(a => a.User)
                                 .OrderByDescending(a => a.CreatedAt)
                                 .Take(count)
                                 .ToListAsync();

    public async Task<IEnumerable<AuditLog>> GetByUserAsync(int userId) =>
        await _context.AuditLogs.Where(a => a.UserId == userId)
                                 .OrderByDescending(a => a.CreatedAt)
                                 .ToListAsync();

    public async Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
        string? module, string? search, int skip, int take)
    {
        var query = _context.AuditLogs.Include(a => a.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(a => a.Module == module);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            // EF.Functions.Like keeps this in SQL. A plain string.Contains would also translate,
            // but Like states the intent and matches the case-insensitive collation the columns
            // already use.
            query = query.Where(a =>
                EF.Functions.Like(a.Action, $"%{term}%") ||
                EF.Functions.Like(a.Module, $"%{term}%") ||
                (a.EntityName != null && EF.Functions.Like(a.EntityName, $"%{term}%")) ||
                (a.User != null && EF.Functions.Like(a.User.Username, $"%{term}%")));
        }

        var total = await query.CountAsync();

        // Id descending as the tie-breaker: several entries can share a CreatedAt to the
        // millisecond, and without it one of them can be skipped between pages.
        var items = await query
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip(skip)
            .Take(take > 0 ? take : int.MaxValue)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IEnumerable<AuditLog>> GetByModuleAsync(string module, int count = 100) =>
        await _context.AuditLogs.Include(a => a.User)
                                 .Where(a => a.Module == module)
                                 .OrderByDescending(a => a.CreatedAt)
                                 .Take(count)
                                 .ToListAsync();
}

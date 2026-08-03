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

    public async Task<IEnumerable<AuditLog>> GetByModuleAsync(string module) =>
        await _context.AuditLogs.Include(a => a.User)
                                 .Where(a => a.Module == module)
                                 .OrderByDescending(a => a.CreatedAt)
                                 .ToListAsync();
}

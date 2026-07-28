using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>Audit log repository implementation.</summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly AttendanceDbContext _context;
    public AuditLogRepository(AttendanceDbContext context) => _context = context;

    public async Task AddAsync(AuditLog log)
    {
        await _context.AuditLogs.AddAsync(log);
        await _context.SaveChangesAsync();
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

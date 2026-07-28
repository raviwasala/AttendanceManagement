using AttendanceSystem.Domain.Interfaces;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AttendanceSystem.Infrastructure.Repositories;

/// <summary>Generic EF Core repository implementation.</summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AttendanceDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AttendanceDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id) =>
        await _dbSet.FindAsync(id);

    public virtual async Task<IEnumerable<T>> GetAllAsync() =>
        await _dbSet.ToListAsync();

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await _dbSet.Where(predicate).ToListAsync();

    public virtual async Task<T> AddAsync(T entity)
    {
        var entry = await _dbSet.AddAsync(entity);
        return entry.Entity;
    }

    public virtual Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null) _dbSet.Remove(entity);
    }

    public virtual async Task<bool> ExistsAsync(int id) =>
        await _dbSet.FindAsync(id) != null;

    public virtual async Task<int> CountAsync() =>
        await _dbSet.CountAsync();
}

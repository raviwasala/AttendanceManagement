using AttendanceSystem.Domain.Entities;
using System.Linq.Expressions;

namespace AttendanceSystem.Domain.Interfaces;

/// <summary>Generic repository contract — constrained to BaseEntity to enforce int PK contract.</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync(int? page = null, int? pageSize = null);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// One page of matching rows plus the total that matched, both from the database.
    ///
    /// An explicit sort is required, not optional: SQL Server gives no ordering guarantee
    /// without ORDER BY, so paging an unordered query can return the same row on two pages and
    /// skip another entirely. Id is the usual tie-breaker and is applied automatically.
    /// </summary>
    Task<(IEnumerable<T> Items, int TotalCount)> FindPagedAsync<TKey>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TKey>> orderBy,
        bool descending,
        int skip,
        int take);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
}

using AttendanceSystem.Domain.Entities;
using System.Linq.Expressions;

namespace AttendanceSystem.Domain.Interfaces;

/// <summary>Generic repository contract — constrained to BaseEntity to enforce int PK contract.</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync(int? page = null, int? pageSize = null);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
}

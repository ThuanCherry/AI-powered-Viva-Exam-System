using System.Linq.Expressions;
using AIVES.DataAccessLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace AIVES.DataAccessLayer.Repositories;

/// <summary>
/// Generic repository implementation using Entity Framework Core.
/// Supports Query and Command operations as shown in architecture diagram.
/// </summary>
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly AivesDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(AivesDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // Query operations
    public async Task<T?> GetByIdAsync(long id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null
            ? await _dbSet.CountAsync()
            : await _dbSet.CountAsync(predicate);
    }

    // Command operations
    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }
}

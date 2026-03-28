using System.Linq.Expressions;
using Eascess_Domain.Interfaces;
using Eascess_Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly EaccessDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(EaccessDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id) =>
        await _dbSet.FindAsync(id);

    public async Task<IEnumerable<T>> GetAllAsync() =>
        await _dbSet.ToListAsync();

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await _dbSet.Where(predicate).ToListAsync();

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) =>
        await _dbSet.FirstOrDefaultAsync(predicate);

    public async Task AddAsync(T entity) =>
        await _dbSet.AddAsync(entity);

    public void Update(T entity) =>
        _dbSet.Update(entity);

    public void Delete(T entity) =>
        _dbSet.Remove(entity);
}
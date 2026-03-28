using Eascess_Domain.Interfaces;
using Eascess_Infrastructure.Persistence;

namespace Eascess_Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly EaccessDbContext _context;

    public UnitOfWork(EaccessDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync() =>
        await _context.SaveChangesAsync();

    public void Dispose() =>
        _context.Dispose();
}
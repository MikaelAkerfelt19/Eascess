namespace Eascess_Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
}
using Eascess_Domain.Entities;
using Eascess_Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eascess.Tests.Integration;

/// <summary>
/// Integration testleri için gerçek HTTP pipeline'ı çalıştıran test host.
/// SQL Server yerine InMemory DB kullanır.
/// </summary>
public class EaccessWebAppFactory : WebApplicationFactory<Program>
{
    // Her factory instance'ı kendi sabit DB adını kullanır —
    // böylece SeedDatabase ve HTTP istekleri aynı InMemory DB'ye erişir.
    private readonly string _dbName = $"EaccessTest-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // EF Core'un SQL Server provider'ını ve tüm ilişkili konfigürasyonlarını kaldır
            services.RemoveAll<DbContextOptions<EaccessDbContext>>();
            services.RemoveAll<EaccessDbContext>();

            // IDbContextOptionsConfiguration<T> tipindeki kayıtları da temizle
            var optionConfigs = services
                .Where(d => d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition().FullName?
                                .Contains("IDbContextOptionsConfiguration") == true)
                .ToList();
            foreach (var d in optionConfigs) services.Remove(d);

            // Sabit adlı InMemory DB ile yeniden kaydet
            services.AddDbContext<EaccessDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Test DB'sine seed data ekler.
    /// </summary>
    public void SeedDatabase(Action<EaccessDbContext> seeder)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EaccessDbContext>();
        db.Database.EnsureCreated();
        seeder(db);
        db.SaveChanges();
    }
}

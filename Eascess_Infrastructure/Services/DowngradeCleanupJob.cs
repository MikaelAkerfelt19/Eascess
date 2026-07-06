using Eascess_Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eascess_Infrastructure.Services;

/// <summary>
/// Her gün 00:05 UTC'de çalışır (denemeler gece 00:00'da bittiği için hemen sonrasında).
/// TrialExpiryJob'ın yerini alır ve kapsamı genişletir: yalnızca Pro denemesi değil,
/// süresi dolan TÜM ücretli abonelikler işlenir; Ücretsiz'e düşen kullanıcının
/// domainleri silinir; 60 günü dolan logolar kalıcı temizlenir.
/// Asıl iş mantığı test edilebilir olması için <see cref="IDowngradeCleanupService"/>'tedir.
/// </summary>
public sealed class DowngradeCleanupJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DowngradeCleanupJob> _logger;

    public DowngradeCleanupJob(IServiceProvider services, ILogger<DowngradeCleanupJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // Her gün 00:05 UTC'de çalış
            var next = now.Date.AddDays(1).AddMinutes(5);
            if (now.TimeOfDay < TimeSpan.FromMinutes(5))
                next = now.Date.AddMinutes(5);

            try { await Task.Delay(next - now, ct); }
            catch (OperationCanceledException) { break; }
            if (ct.IsCancellationRequested) break;

            try
            {
                await using var scope = _services.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IDowngradeCleanupService>();
                await service.RunAsync(DateTime.UtcNow, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DowngradeCleanupJob] Hata oluştu.");
            }
        }
    }
}

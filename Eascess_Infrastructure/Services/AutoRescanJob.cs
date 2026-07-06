using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eascess_Infrastructure.Services;

/// <summary>
/// "Sürekli otomatik yeniden tarama" vaadinin kod karşılığı:
/// her gün 03:00 UTC'de, planında otomatik yeniden tarama bulunan (Pro ve üzeri)
/// kullanıcıların doğrulanmış domain'lerini yeniden tarar.
/// Raporlar ScanType="Auto" ile kaydedilir.
/// </summary>
public class AutoRescanJob : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<AutoRescanJob> _logger;

    public AutoRescanJob(IServiceProvider sp, ILogger<AutoRescanJob> logger)
    {
        _sp     = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now  = DateTime.UtcNow;
            var next = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0, DateTimeKind.Utc);
            if (next <= now) next = next.AddDays(1);

            _logger.LogInformation("[AutoRescanJob] Sonraki çalışma: {Next}", next);

            await Task.Delay(next - now, stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AutoRescanJob] Hata oluştu.");
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await using var scope = _sp.CreateAsyncScope();
        var domainRepo  = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();
        var planService = scope.ServiceProvider.GetRequiredService<IPlanService>();
        var scanService = scope.ServiceProvider.GetRequiredService<IScanService>();

        var domains = await domainRepo.FindAsync(d => d.IsDeleted != true && d.IsVerified == true);

        // Plan sorgusu kullanıcı başına bir kez yapılır
        var rescanAllowedByUser = new Dictionary<string, bool>();
        var scanned = 0;
        var skipped = 0;

        foreach (var domain in domains)
        {
            if (ct.IsCancellationRequested) break;

            if (!rescanAllowedByUser.TryGetValue(domain.UserId, out var allowed))
            {
                var plan = await planService.GetUserActivePlanAsync(domain.UserId);
                allowed = plan.HasAutoRescan;
                rescanAllowedByUser[domain.UserId] = allowed;
            }

            if (!allowed)
            {
                skipped++;
                continue;
            }

            try
            {
                var result = await scanService.ScanDomainAsync(domain.Id, domain.UserId, scanType: "Auto");
                if (result.Success) scanned++;
                else _logger.LogWarning("[AutoRescanJob] {Domain} taranamadı: {Error}", domain.DomainUrl, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AutoRescanJob] {Domain} taranırken hata oluştu.", domain.DomainUrl);
            }
        }

        _logger.LogInformation(
            "[AutoRescanJob] Tamamlandı — {Scanned} domain tarandı, {Skipped} domain plan kapsamı dışında atlandı.",
            scanned, skipped);
    }
}

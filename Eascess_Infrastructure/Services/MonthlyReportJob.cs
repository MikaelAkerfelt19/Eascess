using Eascess_Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eascess_Infrastructure.Services;

/// <summary>
/// Her ayın 1'inde saat 08:00 UTC'de tüm doğrulanmış domain'ler için
/// aylık erişilebilirlik raporu üretir ve email ile gönderir.
/// </summary>
public class MonthlyReportJob : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<MonthlyReportJob> _logger;

    public MonthlyReportJob(IServiceProvider sp, ILogger<MonthlyReportJob> logger)
    {
        _sp     = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now  = DateTime.UtcNow;
            // Her gün 08:00 UTC'de kontrol et — ayın 1'i mi?
            var next = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0, DateTimeKind.Utc)
                .AddDays(now.Hour >= 8 ? 1 : 0);

            var delay = next - now;
            _logger.LogInformation("[MonthlyReportJob] Sonraki kontrol: {Next}", next);

            await Task.Delay(delay, stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;

            if (DateTime.UtcNow.Day != 1) continue; // Sadece ayın 1'inde çalış

            var reportYear  = now.Month == 1 ? now.Year - 1 : now.Year;
            var reportMonth = now.Month == 1 ? 12 : now.Month - 1;

            _logger.LogInformation("[MonthlyReportJob] {Year}/{Month} raporları üretiliyor...", reportYear, reportMonth);

            await using var scope = _sp.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IMonthlyReportService>();
            try
            {
                await service.GenerateForAllDomainsAsync(reportYear, reportMonth, stoppingToken);
                _logger.LogInformation("[MonthlyReportJob] Tamamlandı.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MonthlyReportJob] Hata oluştu.");
            }
        }
    }
}

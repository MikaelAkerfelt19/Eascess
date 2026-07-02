using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eascess_Infrastructure.Services;

/// <summary>
/// Her gün 00:05 UTC'de çalışır.
/// Süresi dolmuş Pro trial subscription'larını deaktive eder,
/// o kullanıcının bekleyen Ücretsiz planını aktive eder.
/// </summary>
public sealed class TrialExpiryJob : BackgroundService
{
    private const int ProPlanId = 2;
    private const int FreePlanId = 1;

    private readonly IServiceProvider _services;
    private readonly ILogger<TrialExpiryJob> _logger;

    public TrialExpiryJob(IServiceProvider services, ILogger<TrialExpiryJob> logger)
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

            try { await ExpireTrialsAsync(ct); }
            catch (Exception ex) { _logger.LogError(ex, "TrialExpiryJob başarısız oldu"); }
        }
    }

    private async Task ExpireTrialsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var subscriptionRepo = scope.ServiceProvider.GetRequiredService<IRepository<UserSubscription>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;

        // Süresi dolmuş ama hâlâ aktif olan Pro trial subscription'ları bul
        var expiredTrials = await subscriptionRepo.FindAsync(
            s => s.PlanId == ProPlanId
              && s.IsActive
              && !s.IsDeleted
              && s.EndDate < now);

        var expiredList = expiredTrials.ToList();
        if (expiredList.Count == 0) return;

        _logger.LogInformation("Süresi dolmuş {Count} trial subscription bulundu", expiredList.Count);

        foreach (var trial in expiredList)
        {
            // Pro subscription'ı deaktive et
            trial.IsActive = false;
            subscriptionRepo.Update(trial);

            // Aynı kullanıcının bekleyen Ücretsiz planını aktive et.
            // Not: kayıt sırasında Ücretsiz plan IsActive=true + ileri tarihli
            // StartDate ile oluşturulur; bu yüzden IsActive'e göre filtrelenmez —
            // aksi halde bulunamaz ve her gece mükerrer kayıt oluşur.
            var freeSub = await subscriptionRepo.FirstOrDefaultAsync(
                s => s.UserId == trial.UserId
                  && s.PlanId == FreePlanId
                  && !s.IsDeleted);

            if (freeSub is not null)
            {
                freeSub.IsActive = true;
                if (freeSub.StartDate > now)
                    freeSub.StartDate = now;
                subscriptionRepo.Update(freeSub);
                _logger.LogInformation("Kullanıcı {UserId} Ücretsiz plana geçirildi", trial.UserId);
            }
            else
            {
                // Bekleyen Ücretsiz plan yoksa yeni oluştur
                await subscriptionRepo.AddAsync(new UserSubscription
                {
                    UserId = trial.UserId,
                    PlanId = FreePlanId,
                    StartDate = now,
                    EndDate = now.AddYears(100),
                    IsActive = true,
                    AutoRenew = false,
                    IsDeleted = false,
                });
                _logger.LogInformation("Kullanıcı {UserId} için yeni Ücretsiz plan oluşturuldu", trial.UserId);
            }
        }

        await unitOfWork.SaveChangesAsync();
        _logger.LogInformation("TrialExpiryJob tamamlandı: {Count} kullanıcı işlendi", expiredList.Count);
    }
}

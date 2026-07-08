using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eascess_Infrastructure.Services;

/// <summary>
/// Her gün 09:00 UTC'de çalışır.
/// Denemesi 3 gün sonra sona erecek kullanıcılara hatırlatma e-postası gönderir.
/// </summary>
public sealed class TrialReminderJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TrialReminderJob> _logger;

    public TrialReminderJob(IServiceProvider services, ILogger<TrialReminderJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var next = now.Date.AddDays(1).AddHours(9); // yarın 09:00 UTC
            if (now.Hour < 9) next = now.Date.AddHours(9); // bugün 09:00 henüz geçmediyse bugün

            await Task.Delay(next - now, ct);
            if (ct.IsCancellationRequested) break;

            try { await SendRemindersAsync(ct); }
            catch (Exception ex) { _logger.LogError(ex, "Trial reminder job failed"); }
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var planService = scope.ServiceProvider.GetRequiredService<IPlanService>();

        var targetDate = DateTime.UtcNow.Date.AddDays(3); // 3 gün sonra sona erenler

        var usersToRemind = userManager.Users
            .Where(u => u.TrialEndsAt.HasValue
                     && u.TrialEndsAt.Value.Date == targetDate
                     && u.IsActive && !u.IsDeleted)
            .ToList();

        foreach (var user in usersToRemind)
        {
            if (user.Email is null) continue;

            // Ödeme yapan kullanıcının TrialEndsAt'i kapatıldığı için zaten bu
            // listeye düşmez. Burada yalnızca deneme kademesinin ÜSTÜNE elle
            // taşınmış kullanıcılar elenir — onlara yükseltme çağrısı anlamsız.
            var plan = await planService.GetUserActivePlanAsync(user.Id);
            if (plan.IsAboveTrialTier) continue;

            var html = $"""
                <p>Merhaba {user.FullName ?? user.UserName},</p>
                <p>14 günlük ücretsiz Pro denemenizin bitmesine <strong>3 gün</strong> kaldı.</p>
                <p>Pro avantajlarınızı kaybetmemek için planınızı şimdi yükseltin.</p>
                <p><a href="https://app.eascess.com/Home/Pricing">Planları Karşılaştır →</a></p>
                """;
            try
            {
                await emailService.SendAsync(
                    user.Email,
                    user.FullName ?? user.UserName ?? "",
                    "Deneme süreniz bitiyor — Eascess Pro",
                    html,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Trial reminder email failed for {UserId}", user.Id);
            }
        }
    }
}

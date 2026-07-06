using Eascess_Application.Services;
using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eascess_Infrastructure.Services;

/// <summary>
/// <see cref="IDowngradeCleanupService"/> uygulaması. Her gece 00:05 UTC'de
/// <see cref="DowngradeCleanupJob"/> tarafından çalıştırılır (denemeler gece
/// 00:00'da bittiği için hemen sonrasında).
/// </summary>
public class DowngradeCleanupService : IDowngradeCleanupService
{
    /// <summary>Logo bekleme süresi: son ücretli abonelik bitişinden itibaren 60 gün.</summary>
    private const int LogoRetentionDays = 60;

    private readonly IRepository<UserSubscription> _subRepo;
    private readonly IRepository<Domain> _domainRepo;
    private readonly IRepository<WidgetSetting> _widgetSettingRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPlanService _planService;
    private readonly IHostEnvironment _env;
    private readonly ILogger<DowngradeCleanupService> _logger;

    public DowngradeCleanupService(
        IRepository<UserSubscription> subRepo,
        IRepository<Domain> domainRepo,
        IRepository<WidgetSetting> widgetSettingRepo,
        IUnitOfWork unitOfWork,
        IPlanService planService,
        IHostEnvironment env,
        ILogger<DowngradeCleanupService> logger)
    {
        _subRepo           = subRepo;
        _domainRepo        = domainRepo;
        _widgetSettingRepo = widgetSettingRepo;
        _unitOfWork        = unitOfWork;
        _planService       = planService;
        _env               = env;
        _logger            = logger;
    }

    public async Task<DowngradeCleanupResult> RunAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var (deactivated, affectedUsers) = await DeactivateExpiredPaidSubscriptionsAsync(utcNow);
        var deletedDomains = await DeleteDomainsOfDowngradedUsersAsync(affectedUsers, utcNow);
        var purgedLogos = await PurgeExpiredLogosAsync(utcNow);

        if (deactivated + deletedDomains + purgedLogos > 0)
            _logger.LogInformation(
                "[DowngradeCleanup] {Subs} abonelik pasifleştirildi, {Domains} domain silindi, {Logos} logo temizlendi.",
                deactivated, deletedDomains, purgedLogos);

        return new DowngradeCleanupResult(deactivated, deletedDomains, purgedLogos);
    }

    // ── 1. Süresi dolan ücretli abonelikleri pasifleştir, Ücretsiz'i aktive et ──
    private async Task<(int Deactivated, List<string> AffectedUsers)> DeactivateExpiredPaidSubscriptionsAsync(DateTime utcNow)
    {
        var expired = (await _subRepo.FindAsync(
            s => s.PlanId != PlanIds.Free && s.IsActive && !s.IsDeleted && s.EndDate < utcNow)).ToList();

        if (expired.Count == 0) return (0, new List<string>());

        var processedUsers = new HashSet<string>();

        foreach (var sub in expired)
        {
            sub.IsActive = false;
            _subRepo.Update(sub);

            if (!processedUsers.Add(sub.UserId)) continue;

            // Bekleyen Ücretsiz planı aktive et (kayıt akışı ileri tarihli oluşturur);
            // yoksa yeni oluştur — TrialExpiryJob'dan devralınan davranış.
            var freeSub = await _subRepo.FirstOrDefaultAsync(
                s => s.UserId == sub.UserId && s.PlanId == PlanIds.Free && !s.IsDeleted);

            if (freeSub is not null)
            {
                freeSub.IsActive = true;
                if (freeSub.StartDate > utcNow)
                    freeSub.StartDate = utcNow;
                _subRepo.Update(freeSub);
            }
            else
            {
                await _subRepo.AddAsync(new UserSubscription
                {
                    UserId = sub.UserId,
                    PlanId = PlanIds.Free,
                    StartDate = utcNow,
                    EndDate = utcNow.AddYears(100),
                    IsActive = true,
                    AutoRenew = false,
                    IsDeleted = false,
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return (expired.Count, processedUsers.ToList());
    }

    // ── 2. Ücretsiz'e düşen kullanıcının TÜM domainlerini sil ────────────────
    // Ürün kararı: düşen kullanıcı domainlerini yeniden bağlamak zorundadır.
    private async Task<int> DeleteDomainsOfDowngradedUsersAsync(List<string> affectedUsers, DateTime utcNow)
    {
        var deleted = 0;

        foreach (var userId in affectedUsers)
        {
            // Başka bir ücretli aboneliği hâlâ geçerliyse (ör. Ultra aktifken Pro bitti) dokunma
            var plan = await _planService.GetUserActivePlanAsync(userId);
            if (plan.Id != PlanIds.Free) continue;

            var domains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);
            foreach (var domain in domains)
            {
                domain.IsDeleted = true;
                domain.DeletedAt = utcNow;
                _domainRepo.Update(domain);
                deleted++;

                var settings = await _widgetSettingRepo.FindAsync(w => w.DomainId == domain.Id && w.IsActive);
                foreach (var ws in settings)
                {
                    ws.IsActive = false;
                    _widgetSettingRepo.Update(ws);
                }
            }

            if (deleted > 0)
                _logger.LogInformation("[DowngradeCleanup] {UserId} Ücretsiz'e düştü — domainleri silindi.", userId);
        }

        await _unitOfWork.SaveChangesAsync();
        return deleted;
    }

    // ── 3. 60 günü dolan logoları kalıcı sil ─────────────────────────────────
    // Kullanıcı 60 gün içinde ücretli plana dönerse logo (ve özelleştirme) korunur.
    private async Task<int> PurgeExpiredLogosAsync(DateTime utcNow)
    {
        var withLogo = (await _widgetSettingRepo.FindAsync(
            w => w.LogoUrl != null && w.LogoUrl != "")).ToList();

        if (withLogo.Count == 0) return 0;

        var purged = 0;
        var planCache = new Dictionary<string, Plan>();

        foreach (var setting in withLogo)
        {
            var domain = await _domainRepo.GetByIdAsync(setting.DomainId); // silinmiş domain de dahil
            if (domain is null) continue;

            if (!planCache.TryGetValue(domain.UserId, out var plan))
            {
                plan = await _planService.GetUserActivePlanAsync(domain.UserId);
                planCache[domain.UserId] = plan;
            }

            // Kullanıcı ücretli plana dönmüş → bekleme sıfırlanır, logo kalır
            if (plan.HasWidgetCustomization) continue;

            // Son ücretli abonelik bitişi üzerinden 60 gün geçti mi?
            var paidSubs = await _subRepo.FindAsync(
                s => s.UserId == domain.UserId && s.PlanId != PlanIds.Free && !s.IsDeleted);
            var lastPaidEnd = paidSubs.Select(s => (DateTime?)s.EndDate).DefaultIfEmpty(null).Max();

            if (lastPaidEnd is null) continue; // ücretli geçmişi yok — dokunma
            if (lastPaidEnd > utcNow.AddDays(-LogoRetentionDays)) continue; // bekleme sürüyor

            DeleteLogoFile(setting.LogoUrl!);
            setting.LogoUrl = null;
            _widgetSettingRepo.Update(setting);
            purged++;
        }

        if (purged > 0)
            await _unitOfWork.SaveChangesAsync();

        return purged;
    }

    private void DeleteLogoFile(string logoUrl)
    {
        if (!logoUrl.StartsWith("/uploads/logos/", StringComparison.OrdinalIgnoreCase))
            return; // yalnızca bizim yüklediğimiz yerel dosyalar

        try
        {
            var webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            var logosRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "logos"));
            var filePath = Path.GetFullPath(Path.Combine(webRoot,
                logoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

            // Klasör dışına çıkan URL'leri (path traversal) yok say
            if (filePath.StartsWith(logosRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DowngradeCleanup] Logo dosyası silinemedi: {Url}", logoUrl);
        }
    }
}

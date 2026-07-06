namespace Eascess_Application.Services;

public record DowngradeCleanupResult(
    int DeactivatedSubscriptions,
    int DeletedDomains,
    int PurgedLogos);

/// <summary>
/// Plan düşüşü temizliği (2026-07-06 ürün kararları):
/// 1. Süresi dolan ücretli abonelikler pasifleştirilir, bekleyen Ücretsiz plan aktive edilir.
/// 2. Ücretsiz'e düşen kullanıcının TÜM domainleri silinir — yeniden bağlaması gerekir.
/// 3. Logolar 60 gün bekletilir: kullanıcı bu sürede ücretli plana dönerse kalır,
///    dönmezse dosya ve kayıt kalıcı silinir.
/// </summary>
public interface IDowngradeCleanupService
{
    Task<DowngradeCleanupResult> RunAsync(DateTime utcNow, CancellationToken ct = default);
}

namespace Eascess_Domain.Constants;

/// <summary>
/// Seed edilen plan satırlarının sabit Id'leri.
/// Migration'lardaki Plans seed verisiyle birebir eşleşmelidir.
///
/// NOT (2026-07-06 ürün kararı): İleride yeni plan eklenmesi PLANLANMIYOR.
/// Buna rağmen bir gün yeni plan eklenirse şunlar BİRLİKTE güncellenmelidir:
///  1. Buraya yeni sabit,
///  2. Plan.Capabilities.cs → TierRank ve özellik kapıları,
///  3. Fiyatlandırma sayfası (Views/Home/Pricing.cshtml) kart + karşılaştırma tablosu,
///  4. Plans tablosuna migration ile seed satırı + EaccessDbContext.HasData,
///  5. PlanEnforcementIntegrationTests kullanıcı matrisi ve PlanCapabilitiesTests.
/// Bunlardan biri atlanırsa vaat/uygulama tutarsızlığı oluşur; test matrisi yakalar.
/// </summary>
public static class PlanIds
{
    public const int Free = 1;
    public const int Pro = 2;
    public const int Enterprise = 3;
    public const int Ultra = 4;
}

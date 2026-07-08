using Eascess_Domain.Constants;

namespace Eascess.Models;

/// <summary>Tanıtım penceresinde gösterilen tek bir ayrıcalık.</summary>
/// <param name="Title">Kısa başlık.</param>
/// <param name="Description">Ne işe yaradığını anlatan tek cümle.</param>
public sealed record PlanPerk(string Title, string Description);

/// <summary>
/// Plan değişikliğinden sonra gösterilen tanıtım içeriği.
///
/// Metinler fiyatlandırma sayfasındaki (Views/Home/Pricing.cshtml) vaatlerle
/// ve Plan.Capabilities.cs'teki özellik kapılarıyla tutarlı olmalıdır —
/// biri değişirse diğerleri de güncellenmelidir.
/// </summary>
public static class PlanHighlights
{
    public static string TitleFor(int planId) => planId switch
    {
        PlanIds.Pro => "Pro planınız aktif",
        PlanIds.Ultra => "Ultra planınız aktif",
        PlanIds.Enterprise => "Kurumsal planınız aktif",
        _ => "Planınız güncellendi",
    };

    public static string LeadFor(int planId) => planId switch
    {
        PlanIds.Pro =>
            "Artık daha fazla domain, otomatik yeniden tarama ve detaylı raporlara erişiyorsunuz. " +
            "İşte planınızla birlikte açılan özellikler:",
        PlanIds.Ultra =>
            "En kapsamlı planımızdasınız. Yüksek kotaların yanında e-posta bildirimleri ve " +
            "öncelikli destek de artık sizin:",
        PlanIds.Enterprise =>
            "Kurumsal planınızla sınırsız kullanım ve size özel hizmetler devrede:",
        _ =>
            "Planınızla birlikte açılan özellikler:",
    };

    public static IReadOnlyList<PlanPerk> PerksFor(int planId) => planId switch
    {
        PlanIds.Pro => new[]
        {
            new PlanPerk("3 domain", "Üç ayrı sitenizi tek hesaptan yönetin."),
            new PlanPerk("Aylık 500 YZ görsel taraması", "Eksik alt metinler yapay zekâyla otomatik doldurulur."),
            new PlanPerk("Sürekli otomatik yeniden tarama", "Siteniz arka planda düzenli olarak yeniden denetlenir."),
            new PlanPerk("Widget özelleştirme", "Tema, konum, dil ve logo ayarları açıldı."),
            new PlanPerk("Detaylı sayfa bazlı raporlar", "Hangi sayfada hangi WCAG kuralının ihlal edildiğini görün."),
        },
        PlanIds.Ultra => new[]
        {
            new PlanPerk("10 domain", "On ayrı sitenizi tek hesaptan yönetin."),
            new PlanPerk("Aylık 2.000 YZ görsel taraması", "Pro'nun dört katı yapay zekâ kotası."),
            new PlanPerk("E-posta bildirimleri", "Aylık erişilebilirlik raporunuz otomatik olarak e-postanıza gelir."),
            new PlanPerk("Öncelikli destek", "Destek talepleriniz sırada öne alınır ve önce yanıtlanır."),
            new PlanPerk("Detaylı raporlar ve otomatik tarama", "Pro'daki tüm özellikler aynen geçerli."),
        },
        PlanIds.Enterprise => new[]
        {
            new PlanPerk("Sınırsız domain ve YZ taraması", "Kota takibi olmadan çalışın."),
            new PlanPerk("SSO / SAML entegrasyonu", "Kurumsal kimlik sağlayıcınızla tek oturum açma."),
            new PlanPerk("Özel SLA ve hesap yöneticisi", "Size atanmış bir muhatap ve garantili yanıt süresi."),
            new PlanPerk("Fatura ile ödeme", "Kredi kartı yerine kurumsal faturalandırma."),
        },
        _ => Array.Empty<PlanPerk>(),
    };
}

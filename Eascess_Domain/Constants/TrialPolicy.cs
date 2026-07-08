namespace Eascess_Domain.Constants;

/// <summary>
/// Deneme süresi politikası (2026-07-06 ürün kararı):
/// deneme gün bazlıdır — kayıt günü 1. gün sayılır ve deneme
/// 14. günün sonunda, gece 00:00 UTC'de biter.
/// </summary>
public static class TrialPolicy
{
    public const int TrialDays = 14;

    /// <summary>
    /// Deneme süresince kullanıcıya verilen plan.
    /// DİKKAT: deneme kullanıcısı bu planda göründüğü için "planı ücretli mi"
    /// sorusu deneme kullanıcısını AYIRT ETMEZ. Denemede olan biriyle o plana
    /// parayla geçen biri ancak <see cref="Entities.AppUser.IsTrialActive"/>
    /// ve <see cref="Entities.Plan.IsAboveTrialTier"/> ile ayrılır.
    /// </summary>
    public const int TrialPlanId = PlanIds.Pro;

    /// <summary>Kayıt anına göre deneme bitiş zamanı: 14. günün sonu, 00:00 UTC.</summary>
    public static DateTime TrialEndUtc(DateTime utcNow) => utcNow.Date.AddDays(TrialDays);
}

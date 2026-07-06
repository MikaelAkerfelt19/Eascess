namespace Eascess_Domain.Constants;

/// <summary>
/// Deneme süresi politikası (2026-07-06 ürün kararı):
/// deneme gün bazlıdır — kayıt günü 1. gün sayılır ve deneme
/// 14. günün sonunda, gece 00:00 UTC'de biter.
/// </summary>
public static class TrialPolicy
{
    public const int TrialDays = 14;

    /// <summary>Kayıt anına göre deneme bitiş zamanı: 14. günün sonu, 00:00 UTC.</summary>
    public static DateTime TrialEndUtc(DateTime utcNow) => utcNow.Date.AddDays(TrialDays);
}

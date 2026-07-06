using Eascess_Domain.Constants;

namespace Eascess.Tests.Unit;

/// <summary>
/// Deneme bitişi ürün kararı (2026-07-06): gün bazlı, gece 00:00 UTC'de biter.
/// </summary>
public class TrialPolicyTests
{
    [Fact]
    public void TrialEnd_Geceyarisi_00_00_UTC()
    {
        var kayit = new DateTime(2026, 7, 6, 15, 42, 33, DateTimeKind.Utc);

        var bitis = TrialPolicy.TrialEndUtc(kayit);

        Assert.Equal(TimeSpan.Zero, bitis.TimeOfDay); // tam 00:00
        Assert.Equal(new DateTime(2026, 7, 20), bitis); // kayıt günü 1. gün → 14. günün sonu
    }

    [Fact]
    public void TrialEnd_KayitSaatindenBagimsiz()
    {
        var sabah = new DateTime(2026, 7, 6, 0, 0, 1, DateTimeKind.Utc);
        var gece  = new DateTime(2026, 7, 6, 23, 59, 59, DateTimeKind.Utc);

        // Aynı gün kayıt olan herkesin denemesi aynı anda biter
        Assert.Equal(TrialPolicy.TrialEndUtc(sabah), TrialPolicy.TrialEndUtc(gece));
    }
}

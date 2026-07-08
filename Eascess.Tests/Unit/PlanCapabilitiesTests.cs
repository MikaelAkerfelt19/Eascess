using Eascess_Domain.Constants;
using Eascess_Domain.Entities;

namespace Eascess.Tests.Unit;

/// <summary>
/// Plan bazlı özellik kapıları — fiyatlandırma sayfasındaki karşılaştırma
/// tablosuyla birebir aynı matris.
/// </summary>
public class PlanCapabilitiesTests
{
    private static Plan PlanWithId(int id) => new() { Id = id, Name = "Test" };

    [Theory]
    [InlineData(PlanIds.Free,       false)]
    [InlineData(PlanIds.Pro,        true)]
    [InlineData(PlanIds.Ultra,      true)]
    [InlineData(PlanIds.Enterprise, true)]
    public void WidgetCustomization_ProVeUzeri(int planId, bool expected)
        => Assert.Equal(expected, PlanWithId(planId).HasWidgetCustomization);

    [Theory]
    [InlineData(PlanIds.Free,       false)]
    [InlineData(PlanIds.Pro,        true)]
    [InlineData(PlanIds.Ultra,      true)]
    [InlineData(PlanIds.Enterprise, true)]
    public void DetailedReports_ProVeUzeri(int planId, bool expected)
        => Assert.Equal(expected, PlanWithId(planId).HasDetailedReports);

    [Theory]
    [InlineData(PlanIds.Free,       false)]
    [InlineData(PlanIds.Pro,        true)]
    [InlineData(PlanIds.Ultra,      true)]
    [InlineData(PlanIds.Enterprise, true)]
    public void AutoRescan_ProVeUzeri(int planId, bool expected)
        => Assert.Equal(expected, PlanWithId(planId).HasAutoRescan);

    [Theory]
    [InlineData(PlanIds.Free,       false)]
    [InlineData(PlanIds.Pro,        false)]
    [InlineData(PlanIds.Ultra,      true)]
    [InlineData(PlanIds.Enterprise, true)]
    public void EmailNotifications_YalnizcaUltraVeKurumsal(int planId, bool expected)
        => Assert.Equal(expected, PlanWithId(planId).HasEmailNotifications);

    [Theory]
    [InlineData(PlanIds.Free,       false)]
    [InlineData(PlanIds.Pro,        false)]
    [InlineData(PlanIds.Ultra,      true)]
    [InlineData(PlanIds.Enterprise, true)]
    public void PrioritySupport_YalnizcaUltraVeKurumsal(int planId, bool expected)
        => Assert.Equal(expected, PlanWithId(planId).HasPrioritySupport);

    /// <summary>
    /// Deneme kullanıcısı deneme planında (Pro) görünür. Bu yüzden deneme
    /// şeridini kapatan kapı "plan ücretli mi" DEĞİL, "denemenin verdiğinden
    /// üst kademede mi" olmalıdır — aksi halde her deneme kullanıcısında
    /// şerit gizlenir ve hatırlatma e-postası hiç gönderilmez.
    /// </summary>
    [Theory]
    [InlineData(PlanIds.Free,       false)]
    [InlineData(PlanIds.Pro,        false)] // deneme kademesi — şerit görünmeli
    [InlineData(PlanIds.Ultra,      true)]
    [InlineData(PlanIds.Enterprise, true)]
    public void IsAboveTrialTier_YalnizcaDenemeKademesininUstu(int planId, bool expected)
        => Assert.Equal(expected, PlanWithId(planId).IsAboveTrialTier);

    [Fact]
    public void DenemePlani_DenemeKademesininUstunde_Sayilmaz()
        => Assert.False(PlanWithId(TrialPolicy.TrialPlanId).IsAboveTrialTier);

    [Fact]
    public void TierRank_KademeSirasi_KurumsalEnUstte()
    {
        Assert.True(PlanWithId(PlanIds.Enterprise).TierRank > PlanWithId(PlanIds.Ultra).TierRank);
        Assert.True(PlanWithId(PlanIds.Ultra).TierRank      > PlanWithId(PlanIds.Pro).TierRank);
        Assert.True(PlanWithId(PlanIds.Pro).TierRank        > PlanWithId(PlanIds.Free).TierRank);
    }
}

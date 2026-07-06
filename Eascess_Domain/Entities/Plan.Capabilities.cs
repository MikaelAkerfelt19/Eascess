using Eascess_Domain.Constants;

namespace Eascess_Domain.Entities;

/// <summary>
/// Fiyatlandırma sayfasındaki vaatlerin kod karşılığı — plan bazlı özellik kapıları.
/// Yalnızca get içerdikleri için EF tarafından kolona map edilmezler.
/// Yeni bir özellik kapısı eklerken fiyatlandırma sayfasındaki karşılaştırma
/// tablosuyla tutarlı olduğundan emin olun.
/// </summary>
public partial class Plan
{
    /// <summary>Widget özelleştirme (tema, konum, dil, logo) — Pro ve üzeri.</summary>
    public bool HasWidgetCustomization => Id != PlanIds.Free;

    /// <summary>Detaylı sayfa bazlı raporlar ve widget analitikleri — Pro ve üzeri.</summary>
    public bool HasDetailedReports => Id != PlanIds.Free;

    /// <summary>Sürekli otomatik yeniden tarama — Pro ve üzeri.</summary>
    public bool HasAutoRescan => Id != PlanIds.Free;

    /// <summary>Aylık rapor e-posta bildirimleri — yalnızca Ultra ve Kurumsal.</summary>
    public bool HasEmailNotifications => Id is PlanIds.Ultra or PlanIds.Enterprise;

    /// <summary>
    /// Plan kademesi. Fiyat üzerinden sıralama Kurumsal'da (fiyat=0, teklif usulü)
    /// yanlış sonuç verdiği için kademe açıkça tanımlanır.
    /// </summary>
    public int TierRank => Id switch
    {
        PlanIds.Enterprise => 3,
        PlanIds.Ultra      => 2,
        PlanIds.Pro        => 1,
        _                  => 0,
    };
}

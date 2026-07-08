using Eascess_Domain.Constants;
using Eascess_Domain.Entities;

namespace Eascess_Application.Services;

/// <summary>
/// Kupon doğrulamasının geçici uygulaması — kodlar bellekte sabit tanımlıdır.
///
/// Gerçek kampanya yönetimi geldiğinde bu sınıf bir Coupons tablosuna bakan
/// uygulamayla değiştirilir; ICouponService sözleşmesi ve çağıran kod aynı kalır
/// (Program.cs'te tek satır).
///
/// Kurallar burada da gerçek gibi işletilir: kod normalize edilir, plan/dönem
/// kısıtı uygulanır, indirim MaxDiscountRate ile sınırlanır. Böylece UI durumları
/// (geçerli / geçersiz / kısıtlı) bugün test edilebilir.
/// </summary>
public class StubCouponService : ICouponService
{
    private sealed record CouponDefinition(
        decimal Rate,
        string Label,
        string? RequiredBillingPeriod = null,
        int? RequiredPlanId = null);

    private static readonly Dictionary<string, CouponDefinition> Coupons = new(StringComparer.Ordinal)
    {
        ["EASCESS10"]   = new(0.10m, "%10 indirim"),
        ["WCAG25"]      = new(0.25m, "%25 indirim"),
        ["YILLIK15"]    = new(0.15m, "%15 yıllık ödeme indirimi", RequiredBillingPeriod: BillingPeriods.Yearly),
        ["ULTRA20"]     = new(0.20m, "%20 Ultra indirimi", RequiredPlanId: PlanIds.Ultra),
    };

    public CouponValidationResult Validate(string? code, decimal subtotal, Plan plan, string billingPeriod)
    {
        if (string.IsNullOrWhiteSpace(code))
            return CouponValidationResult.None();

        var normalized = code.Trim().ToUpperInvariant();

        if (!Coupons.TryGetValue(normalized, out var coupon))
            return CouponValidationResult.Invalid("Bu kod geçerli değil.");

        if (coupon.RequiredBillingPeriod is not null && coupon.RequiredBillingPeriod != billingPeriod)
            return CouponValidationResult.Invalid("Bu kod yalnızca yıllık ödemede geçerli.");

        if (coupon.RequiredPlanId is not null && coupon.RequiredPlanId != plan.Id)
            return CouponValidationResult.Invalid($"Bu kod yalnızca {plan.Name} dışındaki bir plan için geçerli.");

        if (subtotal <= 0)
            return CouponValidationResult.Invalid("Bu siparişte indirim uygulanamaz.");

        // İndirim hiçbir koşulda ara toplamın MaxDiscountRate'ini aşamaz —
        // hatalı bir kampanya tanımının tutarı sıfıra düşürmesini engeller.
        var raw = subtotal * coupon.Rate;
        var capped = Math.Min(raw, subtotal * BillingPolicy.MaxDiscountRate);

        return CouponValidationResult.Success(
            normalized,
            BillingPolicy.RoundMoney(capped),
            coupon.Label);
    }
}

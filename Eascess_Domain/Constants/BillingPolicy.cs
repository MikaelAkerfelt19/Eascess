namespace Eascess_Domain.Constants;

/// <summary>
/// Faturalama kuralları — fiyatlandırma sayfasındaki vaatlerin tek kaynağı.
///
/// Tüm plan fiyatları KDV HARİÇ saklanır (Plans.MonthlyPrice). Ödeme ekranındaki
/// tutarlar her zaman burada tanımlı kurallarla SUNUCUDA yeniden hesaplanır;
/// istemciden gelen hiçbir tutara güvenilmez.
/// </summary>
public static class BillingPolicy
{
    /// <summary>Tüm fiyatlandırma TRY üzerinden yapılır.</summary>
    public const string Currency = "TRY";

    /// <summary>
    /// Ülke belirtilmediğinde kullanılan KDV oranı (Türkiye, %20).
    /// Fiyatlar KDV hariç saklandığı için toplamda eklenir.
    /// </summary>
    public const decimal DefaultTaxRate = 0.20m;

    /// <summary>
    /// Fatura ülkesinin KDV oranı. Sipariş anındaki oran PaymentOrder.TaxRate
    /// alanına yazılır; oran sonradan değişse de kesilmiş fatura sabit kalır.
    /// Bilinmeyen ülke kodunda varsayılan orana düşer.
    /// </summary>
    public static decimal TaxRateFor(string? countryCode) => Countries.VatRateFor(countryCode);

    /// <summary>
    /// Yıllık ödemede uygulanan ay çarpanı: 12 ay kullanılır, 10 ay ödenir
    /// ("2 ay hediye" — Pricing.cshtml #yillik bölümü). Pro 600×10=6.000,
    /// Ultra 1.000×10=10.000 — pazarlama sayfasıyla birebir eşleşir.
    /// </summary>
    public const int YearlyBilledMonths = 10;

    /// <summary>Yıllık abonelikte verilen erişim süresi (ay).</summary>
    public const int YearlyAccessMonths = 12;

    /// <summary>Aylık abonelikte verilen erişim süresi (ay).</summary>
    public const int MonthlyAccessMonths = 1;

    /// <summary>Bir kupon indirimi en fazla ara toplamın bu oranı kadar olabilir.</summary>
    public const decimal MaxDiscountRate = 0.75m;

    /// <summary>
    /// Seçilen döneme göre ödenecek ay sayısı.
    /// </summary>
    public static int BilledMonths(string billingPeriod) =>
        billingPeriod == BillingPeriods.Yearly ? YearlyBilledMonths : MonthlyAccessMonths;

    /// <summary>
    /// Seçilen döneme göre aboneliğin kaç ay geçerli olacağı.
    /// </summary>
    public static int AccessMonths(string billingPeriod) =>
        billingPeriod == BillingPeriods.Yearly ? YearlyAccessMonths : MonthlyAccessMonths;

    /// <summary>Para tutarlarını kuruş hassasiyetine yuvarlar (banker's rounding değil).</summary>
    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Faturalama dönemi sabitleri. PaymentOrder.BillingPeriod bu değerleri alır.
/// </summary>
public static class BillingPeriods
{
    public const string Monthly = "Monthly";
    public const string Yearly = "Yearly";

    public static bool IsValid(string? value) =>
        value is Monthly or Yearly;
}

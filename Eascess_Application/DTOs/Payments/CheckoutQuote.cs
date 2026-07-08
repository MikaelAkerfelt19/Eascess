namespace Eascess_Application.DTOs.Payments;

/// <summary>
/// Sipariş özeti kırılımı — TAMAMEN SUNUCUDA, PlanId üzerinden hesaplanır.
/// İstemciden gelen hiçbir tutar bu nesneye girmez; ekranda gösterilen değerler
/// yalnızca buradan okunur.
/// </summary>
public class CheckoutQuote
{
    public int PlanId { get; set; }

    public string PlanName { get; set; } = "";

    /// <summary>"Monthly" | "Yearly"</summary>
    public string BillingPeriod { get; set; } = "";

    public string Currency { get; set; } = "TRY";

    /// <summary>Aylık birim fiyat (KDV hariç) — Plans tablosundan okunur.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Ödenen ay sayısı: aylıkta 1, yıllıkta 10.</summary>
    public int BilledMonths { get; set; }

    /// <summary>Aboneliğin geçerli olacağı ay sayısı: aylıkta 1, yıllıkta 12.</summary>
    public int AccessMonths { get; set; }

    /// <summary>UnitPrice × BilledMonths (KDV hariç).</summary>
    public decimal Subtotal { get; set; }

    public string? CouponCode { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>Kupon uygulandıysa kullanıcıya gösterilecek açıklama, ör. "%25 indirim".</summary>
    public string? DiscountLabel { get; set; }

    /// <summary>Subtotal − DiscountAmount. KDV bu tutar üzerinden hesaplanır.</summary>
    public decimal NetAmount { get; set; }

    /// <summary>Fatura ülkesinin ISO 3166-1 alpha-2 kodu — KDV oranı buna göre belirlenir.</summary>
    public string CountryCode { get; set; } = "TR";

    /// <summary>Ülkenin ekranda gösterilecek adı.</summary>
    public string CountryName { get; set; } = "";

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>Tahsil edilecek nihai tutar (KDV dahil).</summary>
    public decimal Total { get; set; }

    /// <summary>Yıllık seçimde aylığa göre kazanılan tutar (KDV hariç). Aylıkta 0.</summary>
    public decimal YearlySavings { get; set; }
}

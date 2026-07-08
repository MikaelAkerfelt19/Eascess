namespace Eascess_Application.DTOs.Payments;

/// <summary>
/// Sağlayıcıdan bağımsız ödeme başlatma isteği.
///
/// Bu nesne CheckoutService tarafından SUNUCUDA üretilir; alanların hiçbiri
/// doğrudan istemciden gelmez. Tutarlar PlanId üzerinden yeniden hesaplanmıştır.
/// Kart bilgisi ALANI YOKTUR ve eklenmemelidir — hosted/redirect akışı
/// tercih edilir, kart verisi hiçbir zaman bizim sistemimize girmez.
/// </summary>
public class PaymentRequest
{
    /// <summary>Dışarıya açık sipariş numarası (PaymentOrder.OrderReference).</summary>
    public string OrderReference { get; set; } = "";

    /// <summary>
    /// Çift tahsilat koruması. Sağlayıcı idempotency destekliyorsa bu anahtar
    /// isteğe (genelde bir HTTP başlığı olarak) iletilmelidir.
    /// </summary>
    public string IdempotencyKey { get; set; } = "";

    /// <summary>Tahsil edilecek nihai tutar — KDV dahil.</summary>
    public decimal Amount { get; set; }

    /// <summary>KDV hariç net tutar (indirim düşülmüş). Sağlayıcı kırılım istiyorsa kullanılır.</summary>
    public decimal NetAmount { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>ISO 4217 para birimi kodu, ör. "TRY".</summary>
    public string Currency { get; set; } = "TRY";

    public int PlanId { get; set; }

    public string PlanName { get; set; } = "";

    /// <summary>"Monthly" | "Yearly"</summary>
    public string BillingPeriod { get; set; } = "";

    public PaymentBuyer Buyer { get; set; } = new();

    public PaymentBillingAddress BillingAddress { get; set; } = new();

    /// <summary>Sepet kalemleri. Tutarları toplamı NetAmount'a eşittir.</summary>
    public IReadOnlyList<PaymentBasketItem> BasketItems { get; set; } = new List<PaymentBasketItem>();

    /// <summary>
    /// Sağlayıcının ödeme sonrası kullanıcıyı geri göndereceği mutlak URL.
    /// Ör: https://app.eascess.io/Checkout/Callback
    /// </summary>
    public string CallbackUrl { get; set; } = "";

    /// <summary>Kullanıcının isteği başlattığı IP — sağlayıcı risk analizi için ister.</summary>
    public string? BuyerIpAddress { get; set; }
}

namespace Eascess_Application.DTOs.Payments;

/// <summary>
/// Sepet kalemi. Sağlayıcıların çoğu (iyzico, PayTR, Stripe line items)
/// kalem kırılımı ister; tutarların toplamı PaymentRequest.NetAmount'a eşittir.
/// </summary>
public class PaymentBasketItem
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Sağlayıcı kategorisi, ör. "Abonelik".</summary>
    public string Category { get; set; } = "";

    /// <summary>KDV hariç kalem tutarı. İndirim kalemlerinde negatif olabilir.</summary>
    public decimal Price { get; set; }
}

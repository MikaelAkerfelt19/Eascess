namespace Eascess_Application.DTOs.Payments;

/// <summary>Fatura adresi. Çoğu sağlayıcı bu alanları zorunlu tutar.</summary>
public class PaymentBillingAddress
{
    public string ContactName { get; set; } = "";

    public string Country { get; set; } = "";

    public string City { get; set; } = "";

    public string Address { get; set; } = "";
}

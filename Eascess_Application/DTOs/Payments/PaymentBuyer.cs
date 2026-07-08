namespace Eascess_Application.DTOs.Payments;

/// <summary>Ödemeyi yapan kişi. Kart bilgisi taşımaz.</summary>
public class PaymentBuyer
{
    /// <summary>Sistemdeki kullanıcı kimliği — sağlayıcıda alıcı eşleştirmesi için.</summary>
    public string UserId { get; set; } = "";

    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Phone { get; set; } = "";

    /// <summary>Kurumsal fatura talep edildiyse true.</summary>
    public bool IsCompany { get; set; }

    public string? CompanyName { get; set; }

    public string? TaxOffice { get; set; }

    public string? TaxNumber { get; set; }
}

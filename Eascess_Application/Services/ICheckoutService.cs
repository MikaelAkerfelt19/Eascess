using Eascess_Application.DTOs.Payments;
using Eascess_Domain.Entities;

namespace Eascess_Application.Services;

/// <summary>
/// Ödeme akışının sunucu tarafı — fiyat hesabı, sipariş yaşam döngüsü ve
/// başarılı ödeme sonrası abonelik açılışı.
///
/// Bu servis ödeme sağlayıcısını TANIMAZ. Sağlayıcıyla konuşmak
/// IPaymentProvider'ın işidir; burada yalnızca sipariş durumu yönetilir.
/// </summary>
public interface ICheckoutService
{
    /// <summary>
    /// Satın alınabilir planı döndürür. Ücretsiz ve teklif usulü (Kurumsal)
    /// planlar satın alınamaz; bulunamazsa null döner.
    /// </summary>
    Task<Plan?> GetPurchasablePlanAsync(int planId);

    /// <summary>
    /// Sipariş özetini SUNUCUDA hesaplar. Tutarların tek üretim noktası burasıdır.
    /// KDV oranı <paramref name="countryCode"/> ile belirlenir; boş bırakılırsa
    /// varsayılan ülke (Türkiye) kullanılır.
    /// </summary>
    Task<CheckoutQuote?> BuildQuoteAsync(
        int planId, string billingPeriod, string? couponCode = null, string? countryCode = null);

    /// <summary>
    /// Kupon kodunu doğrular — "Uygula" eylemi bunu çağırır.
    /// </summary>
    Task<CouponValidationResult> ValidateCouponAsync(int planId, string billingPeriod, string? couponCode);

    /// <summary>
    /// Siparişi oluşturur veya idempotency anahtarıyla eşleşen mevcut siparişi döndürür.
    /// Aynı anahtarla ikinci kez çağrılmak çift sipariş (dolayısıyla çift tahsilat) üretmez.
    /// </summary>
    Task<CheckoutOrderResult> CreateOrGetOrderAsync(CreateOrderCommand command);

    /// <summary>Sipariş numarasıyla siparişi getirir. userId verilirse sahiplik de doğrulanır.</summary>
    Task<PaymentOrder?> GetOrderAsync(string orderReference, string? userId = null);

    /// <summary>Sağlayıcıya yönlendirildi — sipariş Pending'e alınır.</summary>
    Task MarkPendingAsync(PaymentOrder order, string providerName, string? providerTransactionId);

    /// <summary>
    /// Ödemeyi tamamlar: siparişi Paid yapar, aboneliği açar, Payment ve Invoice kayıtlarını üretir.
    /// Sipariş zaten terminal durumdaysa hiçbir şey yapılmaz (tekrarlanan callback güvenliği).
    /// </summary>
    Task<OrderCompletionResult> CompleteOrderAsync(PaymentOrder order, PaymentResult result);

    /// <summary>Siparişi başarısız olarak kapatır. Terminal durumdaki sipariş değiştirilmez.</summary>
    Task FailOrderAsync(PaymentOrder order, string? errorCode, string? errorMessage);
}

/// <summary>Sipariş oluşturma girdisi — tutar alanı YOKTUR, sunucuda hesaplanır.</summary>
public class CreateOrderCommand
{
    public string UserId { get; set; } = "";
    public int PlanId { get; set; }
    public string BillingPeriod { get; set; } = "";
    public string? CouponCode { get; set; }

    /// <summary>Formda taşınan çift gönderim jetonu — idempotency anahtarı bundan türetilir.</summary>
    public string ClientToken { get; set; } = "";

    public string BillingFullName { get; set; } = "";
    public string BillingEmail { get; set; } = "";
    public string BillingPhone { get; set; } = "";
    public string BillingCountry { get; set; } = "";
    public string BillingCity { get; set; } = "";
    public string BillingAddress { get; set; } = "";

    public bool IsCompany { get; set; }
    public string? CompanyName { get; set; }
    public string? TaxOffice { get; set; }
    public string? TaxNumber { get; set; }
}

public class CheckoutOrderResult
{
    public PaymentOrder? Order { get; init; }

    /// <summary>Mevcut bir sipariş bulundu (yeni oluşturulmadı) — çift gönderim.</summary>
    public bool WasExisting { get; init; }

    /// <summary>Sipariş zaten ödenmiş — kullanıcı doğrudan başarı sayfasına gönderilir.</summary>
    public bool AlreadyPaid { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsSuccess => Order is not null && ErrorMessage is null;

    public static CheckoutOrderResult Fail(string message) => new() { ErrorMessage = message };
}

public class OrderCompletionResult
{
    public bool Completed { get; init; }

    /// <summary>Sipariş bu çağrıdan önce zaten tamamlanmıştı — işlem tekrarlanmadı.</summary>
    public bool WasAlreadyCompleted { get; init; }

    public string? ErrorMessage { get; init; }
}

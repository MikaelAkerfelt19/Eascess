namespace Eascess_Application.DTOs.Payments;

/// <summary>
/// Sağlayıcıdan bağımsız ödeme sonucu. Hem CreatePaymentAsync hem
/// VerifyCallbackAsync bu tipi döndürür.
///
/// Sağlayıcıya özgü hiçbir alan taşımaz — yeni bir sağlayıcıya geçildiğinde
/// yalnızca adaptör değişir, çağıran kod aynı kalır.
/// </summary>
public class PaymentResult
{
    public PaymentResultStatus Status { get; set; }

    /// <summary>Sağlayıcının işlem numarası. Mutabakat ve iade için saklanır.</summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>
    /// Status = RedirectRequired ise kullanıcının yönlendirileceği mutlak URL.
    /// HtmlContent ile birlikte yalnızca biri dolu olur.
    /// </summary>
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// Status = RedirectRequired ise tarayıcıya basılacak HTML (3D Secure formu gibi).
    /// Sağlayıcıdan gelir; yalnızca sağlayıcı imzası doğrulanmış akışlarda kullanılır.
    /// </summary>
    public string? HtmlContent { get; set; }

    /// <summary>Makine tarafından okunabilir hata kodu (sağlayıcı kodu olabilir).</summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Kullanıcıya gösterilebilecek hata açıklaması.
    /// Sağlayıcı ham yanıtını veya sır içeren bilgiyi ASLA buraya koymayın.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Sağlayıcının doğruladığı tutar. Callback'te sipariş tutarıyla
    /// karşılaştırılır — eşleşmezse ödeme kabul edilmez.
    /// </summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>Callback'te siparişi bulmak için sağlayıcının geri döndürdüğü sipariş numarası.</summary>
    public string? OrderReference { get; set; }

    public bool IsSuccess => Status == PaymentResultStatus.Succeeded;

    public static PaymentResult Redirect(string url, string? transactionId = null, string? orderReference = null) =>
        new()
        {
            Status = PaymentResultStatus.RedirectRequired,
            RedirectUrl = url,
            ProviderTransactionId = transactionId,
            OrderReference = orderReference,
        };

    public static PaymentResult Html(string html, string? transactionId = null, string? orderReference = null) =>
        new()
        {
            Status = PaymentResultStatus.RedirectRequired,
            HtmlContent = html,
            ProviderTransactionId = transactionId,
            OrderReference = orderReference,
        };

    public static PaymentResult Success(string transactionId, decimal paidAmount, string? orderReference = null) =>
        new()
        {
            Status = PaymentResultStatus.Succeeded,
            ProviderTransactionId = transactionId,
            PaidAmount = paidAmount,
            OrderReference = orderReference,
        };

    public static PaymentResult Failure(string errorCode, string errorMessage, string? orderReference = null) =>
        new()
        {
            Status = PaymentResultStatus.Failed,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            OrderReference = orderReference,
        };

    /// <summary>İmza doğrulaması başarısız — sonuç sahte olabilir, ASLA ödeme sayılmaz.</summary>
    public static PaymentResult InvalidSignature(string? orderReference = null) =>
        new()
        {
            Status = PaymentResultStatus.SignatureInvalid,
            ErrorCode = "signature_invalid",
            ErrorMessage = "Ödeme doğrulaması yapılamadı.",
            OrderReference = orderReference,
        };
}

public enum PaymentResultStatus
{
    /// <summary>Ödeme tamamlandı ve doğrulandı.</summary>
    Succeeded = 0,

    /// <summary>Kullanıcı sağlayıcının sayfasına/formuna yönlendirilmeli.</summary>
    RedirectRequired = 1,

    /// <summary>Sağlayıcı sonucu bekliyor (asenkron bildirim gelecek).</summary>
    Pending = 2,

    /// <summary>Ödeme reddedildi veya hata aldı.</summary>
    Failed = 3,

    /// <summary>Callback imzası doğrulanamadı — güvenlik olayı olarak loglanır.</summary>
    SignatureInvalid = 4,
}

using Eascess_Application.DTOs.Payments;

namespace Eascess_Application.Services;

/// <summary>
/// Ödeme sağlayıcısı soyutlaması. Uygulamanın geri kalanı bu arayüzden başka
/// hiçbir ödeme tipini tanımaz — sağlayıcı değişimi Program.cs'te tek satırdır.
///
/// SÖZLEŞME (uygulayanların uyması zorunlu):
///  1. Kart verisi bu arayüzden GEÇMEZ. Hosted/redirect akışı tercih edilir.
///  2. CreatePaymentAsync tutarı yeniden hesaplamaz; PaymentRequest.Amount
///     zaten sunucuda üretilmiştir ve olduğu gibi sağlayıcıya iletilir.
///  3. VerifyCallbackAsync, sonucu değerlendirmeden ÖNCE imzayı doğrulamak
///     ZORUNDADIR. İmza geçersizse PaymentResult.InvalidSignature() döner —
///     doğrulanmamış bir callback asla başarılı ödeme sayılmaz.
///  4. Hiçbir uygulama API anahtarını, sırrı veya sağlayıcı ham yanıtını
///     PaymentResult.ErrorMessage içine koymaz.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Yapılandırmada seçilen sağlayıcıyla eşleşen ad (Payments:Provider).
    /// PaymentOrder ve Payment kayıtlarına yazılır.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sağlayıcıda ödeme oturumu açar.
    /// Dönüş: RedirectRequired (RedirectUrl veya HtmlContent dolu), Succeeded veya Failed.
    /// </summary>
    Task<PaymentResult> CreatePaymentAsync(PaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sağlayıcının geri dönüş (callback) isteğini doğrular ve sonuca çevirir.
    /// İmza doğrulaması bu metodun içinde yapılır; başarısızsa SignatureInvalid döner.
    /// </summary>
    Task<PaymentResult> VerifyCallbackAsync(PaymentCallbackContext callback, CancellationToken ct = default);
}

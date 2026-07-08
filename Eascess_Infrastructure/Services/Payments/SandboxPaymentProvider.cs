using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Eascess_Application.DTOs.Payments;
using Eascess_Application.Options;
using Eascess_Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eascess_Infrastructure.Services.Payments;

/// <summary>
/// Gerçek sağlayıcı bağlanana kadar tüm ödeme akışını çalıştırılabilir kılan
/// sahte sağlayıcı. GERÇEK TAHSİLAT YAPMAZ.
///
/// Gerçek bir sağlayıcıyı taklit eder, çünkü akışın doğruluğu buna bağlıdır:
///  • Yönlendirme (hosted page) akışını kullanır — kart verisi bizim formumuza girmez.
///  • Callback'i HMAC-SHA256 ile İMZALAR ve VerifyCallbackAsync bu imzayı gerçekten
///    doğrular. Böylece imza doğrulama yolu bugün test edilir; gerçek sağlayıcıya
///    geçildiğinde yalnızca imza algoritması değişir, akış aynı kalır.
///  • Başarı ve başarısızlık senaryolarının ikisini de üretebilir.
///
/// Üretimde kullanılmamalıdır — Payments:Provider değerini gerçek sağlayıcıya çevirin.
/// </summary>
public class SandboxPaymentProvider : IPaymentProvider
{
    /// <summary>
    /// Sandbox imzası için yedek anahtar. SecretKey ayarlanmadığında kullanılır;
    /// yalnızca sahte sağlayıcı içindir, gerçek bir sır DEĞİLDİR ve gerçek
    /// sağlayıcı akışında hiçbir yerde kullanılmaz.
    /// </summary>
    private const string FallbackSandboxSecret = "eascess-sandbox-signing-key";

    private readonly PaymentOptions _options;
    private readonly ILogger<SandboxPaymentProvider> _logger;

    public SandboxPaymentProvider(IOptions<PaymentOptions> options, ILogger<SandboxPaymentProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Sandbox";

    public Task<PaymentResult> CreatePaymentAsync(PaymentRequest request, CancellationToken ct = default)
    {
        // Tutar/para birimi gibi temel doğrulamalar gerçek sağlayıcıda da yapılır.
        if (request.Amount <= 0)
        {
            return Task.FromResult(PaymentResult.Failure(
                "invalid_amount", "Geçersiz ödeme tutarı.", request.OrderReference));
        }

        var transactionId = $"SBX-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";

        _logger.LogInformation(
            "[Sandbox] Ödeme oturumu açıldı. Sipariş={OrderReference} Tutar={Amount} {Currency} İşlem={TransactionId}",
            request.OrderReference, request.Amount, request.Currency, transactionId);

        // Testlerde ara ekran istenmeyebilir: doğrudan başarılı sonuç döner.
        if (_options.SandboxAutoApprove)
        {
            return Task.FromResult(PaymentResult.Success(
                transactionId, request.Amount, request.OrderReference));
        }

        // Sağlayıcının barındırılan ödeme sayfasının karşılığı: kullanıcı uygulamadan
        // çıkar, sahte sağlayıcı ekranında onaylar/reddeder ve callback'e döner.
        var redirectUrl = BuildSandboxPageUrl(request, transactionId);

        return Task.FromResult(PaymentResult.Redirect(
            redirectUrl, transactionId, request.OrderReference));
    }

    public Task<PaymentResult> VerifyCallbackAsync(PaymentCallbackContext callback, CancellationToken ct = default)
    {
        var fields = callback.Form.Count > 0 ? callback.Form : callback.Query;

        fields.TryGetValue("orderReference", out var orderReference);
        fields.TryGetValue("status", out var status);
        fields.TryGetValue("transactionId", out var transactionId);
        fields.TryGetValue("amount", out var amountRaw);
        fields.TryGetValue("signature", out var signature);

        // İMZA DOĞRULAMASI — sonuç değerlendirilmeden önce yapılır.
        // Doğrulanmamış bir callback asla ödeme sayılmaz.
        var expected = ComputeSignature(orderReference, status, transactionId, amountRaw);

        if (string.IsNullOrEmpty(signature) || !FixedTimeEquals(signature, expected))
        {
            _logger.LogWarning(
                "[Sandbox] Callback imzası doğrulanamadı. Sipariş={OrderReference}", orderReference);
            return Task.FromResult(PaymentResult.InvalidSignature(orderReference));
        }

        if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return Task.FromResult(PaymentResult.Failure(
                "invalid_amount", "Ödeme tutarı okunamadı.", orderReference));
        }

        if (status == "success")
        {
            _logger.LogInformation(
                "[Sandbox] Ödeme onaylandı. Sipariş={OrderReference} İşlem={TransactionId}",
                orderReference, transactionId);

            return Task.FromResult(PaymentResult.Success(
                transactionId ?? "", amount, orderReference));
        }

        _logger.LogInformation(
            "[Sandbox] Ödeme reddedildi. Sipariş={OrderReference}", orderReference);

        return Task.FromResult(PaymentResult.Failure(
            "declined", "Ödeme sağlayıcı tarafından reddedildi.", orderReference));
    }

    /// <summary>
    /// Sahte sağlayıcı ekranının imzalı bağlantısı. İmza burada üretilir ki
    /// ekran, callback'e geçerli bir imzayla dönebilsin.
    /// </summary>
    private string BuildSandboxPageUrl(PaymentRequest request, string transactionId)
    {
        var callbackUri = new Uri(request.CallbackUrl);
        var origin = callbackUri.GetLeftPart(UriPartial.Authority);
        var amount = request.Amount.ToString(CultureInfo.InvariantCulture);

        var successSignature = ComputeSignature(request.OrderReference, "success", transactionId, amount);
        var failureSignature = ComputeSignature(request.OrderReference, "failure", transactionId, amount);

        return $"{origin}/Checkout/Sandbox" +
               $"?orderReference={Uri.EscapeDataString(request.OrderReference)}" +
               $"&transactionId={Uri.EscapeDataString(transactionId)}" +
               $"&amount={Uri.EscapeDataString(amount)}" +
               $"&currency={Uri.EscapeDataString(request.Currency)}" +
               $"&successSignature={Uri.EscapeDataString(successSignature)}" +
               $"&failureSignature={Uri.EscapeDataString(failureSignature)}";
    }

    private string ComputeSignature(string? orderReference, string? status, string? transactionId, string? amount)
    {
        var secret = string.IsNullOrWhiteSpace(_options.SecretKey)
            ? FallbackSandboxSecret
            : _options.SecretKey;

        var payload = $"{orderReference}|{status}|{transactionId}|{amount}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>İmza karşılaştırması sabit sürelidir — zamanlama saldırısını engeller.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

using Eascess_Application.DTOs.Payments;
using Eascess_Application.Options;
using Eascess_Infrastructure.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Eascess.Tests.Unit;

/// <summary>
/// Sahte sağlayıcının imza doğrulama yolu için testler.
///
/// Bu testler asıl olarak IPaymentProvider SÖZLEŞMESİNİ kilitler: doğrulanmamış
/// bir callback hiçbir koşulda başarılı ödeme sayılmaz. Gerçek sağlayıcı
/// bağlandığında aynı davranış ondan da beklenir.
/// </summary>
public class SandboxPaymentProviderTests
{
    private const string Secret = "test-secret";

    private static SandboxPaymentProvider Create(bool autoApprove = false) =>
        new(Options.Create(new PaymentOptions
        {
            Provider = "Sandbox",
            SecretKey = Secret,
            SandboxAutoApprove = autoApprove,
        }), NullLogger<SandboxPaymentProvider>.Instance);

    private static PaymentRequest Request(decimal amount = 720m) => new()
    {
        OrderReference = "EA-20260808-ABC123",
        IdempotencyKey = "key-1",
        Amount = amount,
        Currency = "TRY",
        CallbackUrl = "https://app.eascess.io/Checkout/Callback",
    };

    /// <summary>Sandbox ekranının callback'e göndereceği alanları üretir.</summary>
    private static async Task<Dictionary<string, string>> SignedFieldsAsync(
        SandboxPaymentProvider provider, PaymentRequest request, string status)
    {
        var created = await provider.CreatePaymentAsync(request);
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(created.RedirectUrl!).Query);

        return new Dictionary<string, string>
        {
            ["orderReference"] = query["orderReference"]!,
            ["transactionId"] = query["transactionId"]!,
            ["amount"] = query["amount"]!,
            ["status"] = status,
            ["signature"] = query[status == "success" ? "successSignature" : "failureSignature"]!,
        };
    }

    [Fact]
    public async Task CreatePayment_YönlendirmeDöner_KartAlanıİstemez()
    {
        var result = await Create().CreatePaymentAsync(Request());

        Assert.Equal(PaymentResultStatus.RedirectRequired, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.RedirectUrl));
        Assert.Contains("/Checkout/Sandbox", result.RedirectUrl);
    }

    [Fact]
    public async Task CreatePayment_SıfırTutar_Reddedilir()
    {
        var result = await Create().CreatePaymentAsync(Request(amount: 0m));

        Assert.Equal(PaymentResultStatus.Failed, result.Status);
        Assert.Equal("invalid_amount", result.ErrorCode);
    }

    [Fact]
    public async Task CreatePayment_OtomatikOnay_DoğrudanBaşarı()
    {
        var result = await Create(autoApprove: true).CreatePaymentAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal(720m, result.PaidAmount);
    }

    [Fact]
    public async Task VerifyCallback_GeçerliİmzaBaşarı_Onaylanır()
    {
        var provider = Create();
        var fields = await SignedFieldsAsync(provider, Request(), "success");

        var result = await provider.VerifyCallbackAsync(new PaymentCallbackContext { Form = fields });

        Assert.True(result.IsSuccess);
        Assert.Equal(720m, result.PaidAmount);
        Assert.Equal("EA-20260808-ABC123", result.OrderReference);
    }

    [Fact]
    public async Task VerifyCallback_GeçerliİmzaRet_BaşarısızDöner()
    {
        var provider = Create();
        var fields = await SignedFieldsAsync(provider, Request(), "failure");

        var result = await provider.VerifyCallbackAsync(new PaymentCallbackContext { Form = fields });

        Assert.Equal(PaymentResultStatus.Failed, result.Status);
        Assert.Equal("declined", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyCallback_TutarDeğiştirilmiş_İmzaGeçersiz()
    {
        // Saldırgan tutarı düşürüp ucuza abonelik almaya çalışır.
        var provider = Create();
        var fields = await SignedFieldsAsync(provider, Request(), "success");
        fields["amount"] = "1.00";

        var result = await provider.VerifyCallbackAsync(new PaymentCallbackContext { Form = fields });

        Assert.Equal(PaymentResultStatus.SignatureInvalid, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task VerifyCallback_DurumBaşarıyaÇevrilmiş_İmzaGeçersiz()
    {
        // Reddedilen ödemenin durumu "success" yapılırsa imza tutmaz.
        var provider = Create();
        var fields = await SignedFieldsAsync(provider, Request(), "failure");
        fields["status"] = "success";

        var result = await provider.VerifyCallbackAsync(new PaymentCallbackContext { Form = fields });

        Assert.Equal(PaymentResultStatus.SignatureInvalid, result.Status);
    }

    [Fact]
    public async Task VerifyCallback_İmzaYok_Geçersiz()
    {
        var provider = Create();
        var fields = await SignedFieldsAsync(provider, Request(), "success");
        fields.Remove("signature");

        var result = await provider.VerifyCallbackAsync(new PaymentCallbackContext { Form = fields });

        Assert.Equal(PaymentResultStatus.SignatureInvalid, result.Status);
    }

    [Fact]
    public async Task VerifyCallback_BaşkaAnahtarlaİmzalanmış_Geçersiz()
    {
        // İmza gizli anahtara bağlıdır: farklı anahtar → farklı imza.
        var attacker = new SandboxPaymentProvider(
            Options.Create(new PaymentOptions { SecretKey = "yanlis-anahtar" }),
            NullLogger<SandboxPaymentProvider>.Instance);

        var fields = await SignedFieldsAsync(attacker, Request(), "success");

        var result = await Create().VerifyCallbackAsync(new PaymentCallbackContext { Form = fields });

        Assert.Equal(PaymentResultStatus.SignatureInvalid, result.Status);
    }
}

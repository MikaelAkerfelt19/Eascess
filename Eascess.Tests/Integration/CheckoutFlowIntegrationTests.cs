using Eascess_Application.DTOs.Payments;
using Eascess_Application.Services;
using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Eascess_Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eascess.Tests.Integration;

/// <summary>
/// Ödeme akışının uçtan uca testi: gerçek DI kayıtları, gerçek CheckoutService
/// ve yapılandırmadan seçilen IPaymentProvider (varsayılan: Sandbox) ile
/// sipariş → sağlayıcı → callback → abonelik zinciri.
///
/// Bu test aynı zamanda "sağlayıcı değişimi tek satır" iddiasını doğrular:
/// Program.cs'teki kayıt çözülmezse veya yanlış tipi verirse burada patlar.
/// </summary>
public class CheckoutFlowIntegrationTests : IClassFixture<EaccessWebAppFactory>
{
    private readonly EaccessWebAppFactory _factory;

    // IClassFixture tek bir InMemory DB paylaştırır ve xUnit test sırasını
    // garanti etmez. Her test kendi kullanıcısıyla çalışır — böylece bir testin
    // oluşturduğu abonelik/ödeme kaydı diğerinin sorgusuna karışmaz.
    private const string SuccessUser = "checkout-success";
    private const string DeclinedUser = "checkout-declined";
    private const string RepeatUser = "checkout-repeat";

    public CheckoutFlowIntegrationTests(EaccessWebAppFactory factory)
    {
        _factory = factory;
        _factory.SeedDatabase(db =>
        {
            foreach (var id in new[] { SuccessUser, DeclinedUser, RepeatUser })
            {
                if (db.Users.Any(u => u.Id == id)) continue;

                db.Users.Add(new AppUser
                {
                    Id = id,
                    UserName = $"{id}@example.com",
                    Email = $"{id}@example.com",
                    FullName = "Ada Lovelace",
                });
            }
        });
    }

    [Fact]
    public void PaymentProvider_VarsayilanKayit_SandboxCozulur()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();

        Assert.Equal("Sandbox", provider.ProviderName);
    }

    [Fact]
    public async Task TamAkis_OdemeOnaylanir_AbonelikPaymentVeInvoiceOlusur()
    {
        using var scope = _factory.Services.CreateScope();
        var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();
        var db = scope.ServiceProvider.GetRequiredService<EaccessDbContext>();

        // 1) Sipariş — tutar istemciden değil, Plans tablosundan hesaplanır.
        var created = await checkout.CreateOrGetOrderAsync(NewCommand(SuccessUser, "token-success"));
        Assert.True(created.IsSuccess);

        var order = created.Order!;
        Assert.Equal(600m, order.Subtotal);
        Assert.Equal(720m, order.TotalAmount); // 600 + %20 KDV

        // 2) Sağlayıcıya git — yönlendirme akışı (kart verisi bize hiç girmez).
        var createResult = await provider.CreatePaymentAsync(new PaymentRequest
        {
            OrderReference = order.OrderReference,
            IdempotencyKey = order.IdempotencyKey,
            Amount = order.TotalAmount,
            Currency = order.Currency,
            CallbackUrl = "https://localhost/Checkout/Callback",
        });

        Assert.Equal(PaymentResultStatus.RedirectRequired, createResult.Status);
        await checkout.MarkPendingAsync(order, provider.ProviderName, createResult.ProviderTransactionId);

        // 3) Sağlayıcının imzalı callback'i doğrulanır.
        var callback = await provider.VerifyCallbackAsync(
            CallbackFrom(createResult.RedirectUrl!, "success"));

        Assert.True(callback.IsSuccess);

        // 4) Sipariş tamamlanır: abonelik + ödeme + fatura.
        var completion = await checkout.CompleteOrderAsync(order, callback);
        Assert.True(completion.Completed);

        Assert.Equal(PaymentOrderStatus.Paid, order.Status);

        var subscription = await db.UserSubscriptions
            .SingleAsync(s => s.UserId == SuccessUser && s.PlanId == PlanIds.Pro);
        Assert.True(subscription.IsActive);

        var payment = await db.Payments.SingleAsync(p => p.SubscriptionId == subscription.Id);
        Assert.Equal(720m, payment.Amount);
        Assert.Equal(PaymentStatuses.Succeeded, payment.PaymentStatus);

        var invoice = await db.Invoices.SingleAsync(i => i.PaymentId == payment.Id);
        Assert.True(invoice.IsPaid);
        Assert.Equal(720m, invoice.Amount);
    }

    [Fact]
    public async Task TamAkis_OdemeReddedilir_AbonelikOlusmaz()
    {
        using var scope = _factory.Services.CreateScope();
        var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();
        var db = scope.ServiceProvider.GetRequiredService<EaccessDbContext>();

        var order = (await checkout.CreateOrGetOrderAsync(
            NewCommand(DeclinedUser, "token-declined", PlanIds.Ultra))).Order!;

        var createResult = await provider.CreatePaymentAsync(new PaymentRequest
        {
            OrderReference = order.OrderReference,
            Amount = order.TotalAmount,
            Currency = order.Currency,
            CallbackUrl = "https://localhost/Checkout/Callback",
        });

        await checkout.MarkPendingAsync(order, provider.ProviderName, createResult.ProviderTransactionId);

        var callback = await provider.VerifyCallbackAsync(
            CallbackFrom(createResult.RedirectUrl!, "failure"));

        Assert.False(callback.IsSuccess);
        await checkout.FailOrderAsync(order, callback.ErrorCode, callback.ErrorMessage);

        Assert.Equal(PaymentOrderStatus.Failed, order.Status);
        Assert.False(await db.UserSubscriptions.AnyAsync(s => s.UserId == DeclinedUser));
        Assert.False(await db.Payments.AnyAsync(p => p.UserId == DeclinedUser));
    }

    [Fact]
    public async Task TekrarlananCallback_IkinciAbonelikVeFaturaOlusturmaz()
    {
        using var scope = _factory.Services.CreateScope();
        var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        var db = scope.ServiceProvider.GetRequiredService<EaccessDbContext>();

        var order = (await checkout.CreateOrGetOrderAsync(NewCommand(RepeatUser, "token-repeat"))).Order!;
        await checkout.MarkPendingAsync(order, "Sandbox", "TX-REPEAT");

        var result = PaymentResult.Success("TX-REPEAT", order.TotalAmount, order.OrderReference);

        var first = await checkout.CompleteOrderAsync(order, result);
        var second = await checkout.CompleteOrderAsync(order, result);

        Assert.True(first.Completed);
        Assert.False(first.WasAlreadyCompleted);
        Assert.True(second.WasAlreadyCompleted);

        // Sağlayıcılar aynı bildirimi tekrar gönderebilir — tek kayıt kalmalı.
        Assert.Equal(1, await db.Payments.CountAsync(p => p.TransactionId == "TX-REPEAT"));
    }

    // ── Yardımcılar ────────────────────────────────────────────────

    /// <summary>
    /// Sandbox ekranının callback'e göndereceği imzalı alanları, sağlayıcının
    /// ürettiği yönlendirme URL'sinden çıkarır.
    /// </summary>
    private static PaymentCallbackContext CallbackFrom(string redirectUrl, string status)
    {
        var query = new Uri(redirectUrl).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

        return new PaymentCallbackContext
        {
            Form = new Dictionary<string, string>
            {
                ["orderReference"] = query["orderReference"],
                ["transactionId"] = query["transactionId"],
                ["amount"] = query["amount"],
                ["status"] = status,
                ["signature"] = query[status == "success" ? "successSignature" : "failureSignature"],
            },
        };
    }

    private static CreateOrderCommand NewCommand(string userId, string token, int planId = PlanIds.Pro) => new()
    {
        UserId = userId,
        PlanId = planId,
        BillingPeriod = BillingPeriods.Monthly,
        ClientToken = token,
        BillingFullName = "Ada Lovelace",
        BillingEmail = "checkout@example.com",
        BillingPhone = "+90 555 000 00 00",
        BillingCountry = "TR",
        BillingCity = "İstanbul",
        BillingAddress = "Örnek Mahallesi, Test Sokak No 1",
    };
}

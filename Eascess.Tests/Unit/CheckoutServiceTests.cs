using System.Linq.Expressions;
using Eascess_Application.DTOs.Payments;
using Eascess_Application.Services;
using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Moq;

namespace Eascess.Tests.Unit;

/// <summary>
/// CheckoutService için unit testler.
///
/// Odak: fiyatın SUNUCUDA doğru hesaplanması, çift tahsilat koruması ve
/// terminal sipariş durumlarının korunması. Bunlar bozulursa para hatası olur.
/// </summary>
public class CheckoutServiceTests
{
    private readonly Mock<IRepository<Plan>> _planRepo = new();
    private readonly Mock<IRepository<PaymentOrder>> _orderRepo = new();
    private readonly Mock<IRepository<UserSubscription>> _subRepo = new();
    private readonly Mock<IRepository<Payment>> _paymentRepo = new();
    private readonly Mock<IRepository<Invoice>> _invoiceRepo = new();
    private readonly Mock<IRepository<AppUser>> _userRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly CheckoutService _sut;

    private static readonly Plan FreePlan = new() { Id = PlanIds.Free, Name = "Ücretsiz", MonthlyPrice = 0, IsActive = true };
    private static readonly Plan ProPlan = new() { Id = PlanIds.Pro, Name = "Pro", MonthlyPrice = 600, IsActive = true };
    private static readonly Plan EnterprisePlan = new() { Id = PlanIds.Enterprise, Name = "Kurumsal", MonthlyPrice = 0, IsActive = true };
    private static readonly Plan UltraPlan = new() { Id = PlanIds.Ultra, Name = "Ultra", MonthlyPrice = 1000, IsActive = true };

    private readonly List<PaymentOrder> _orders = new();
    private readonly List<AppUser> _users = new();

    public CheckoutServiceTests()
    {
        var plans = new[] { FreePlan, ProPlan, EnterprisePlan, UltraPlan };

        _planRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Plan, bool>>>()))
                 .ReturnsAsync((Expression<Func<Plan, bool>> p) => plans.FirstOrDefault(p.Compile()));

        // Sipariş deposu bellekte tutulur — idempotency davranışı gerçekçi test edilir.
        _orderRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentOrder, bool>>>()))
                  .ReturnsAsync((Expression<Func<PaymentOrder, bool>> p) => _orders.FirstOrDefault(p.Compile()));
        _orderRepo.Setup(r => r.AddAsync(It.IsAny<PaymentOrder>()))
                  .Callback((PaymentOrder o) => _orders.Add(o))
                  .Returns(Task.CompletedTask);

        // Ücretli plana geçişte kapatılacak Ücretsiz abonelikler sorgulanır —
        // varsayılan olarak kullanıcının Ücretsiz aboneliği yok kabul edilir.
        _subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(Array.Empty<UserSubscription>());

        // Ödeme tamamlandığında alıcının denemesi kapatılır — kullanıcı satırı
        // bellekte tutulur ki testler TrialEndsAt'in düştüğünü görebilsin.
        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<AppUser, bool>>>()))
                 .ReturnsAsync((Expression<Func<AppUser, bool>> p) => _users.FirstOrDefault(p.Compile()));

        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        _sut = new CheckoutService(
            _planRepo.Object, _orderRepo.Object, _subRepo.Object,
            _paymentRepo.Object, _invoiceRepo.Object, _userRepo.Object,
            new StubCouponService(), _uow.Object);
    }

    // ── Satın alınabilirlik ────────────────────────────────────────

    [Theory]
    [InlineData(PlanIds.Free)]        // ücretsiz — satın alınacak bir şey yok
    [InlineData(PlanIds.Enterprise)]  // teklif usulü — fiyatı 0
    public async Task GetPurchasablePlan_FiyatsızPlanlar_Null(int planId)
    {
        Assert.Null(await _sut.GetPurchasablePlanAsync(planId));
    }

    [Fact]
    public async Task GetPurchasablePlan_ÜcretliPlan_Döner()
    {
        var plan = await _sut.GetPurchasablePlanAsync(PlanIds.Pro);
        Assert.NotNull(plan);
        Assert.Equal(600, plan!.MonthlyPrice);
    }

    // ── Fiyat hesabı ───────────────────────────────────────────────

    [Fact]
    public async Task BuildQuote_AylıkPro_KdvEklenir()
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Monthly);

        Assert.NotNull(q);
        Assert.Equal(600m, q!.Subtotal);
        Assert.Equal(0m, q.DiscountAmount);
        Assert.Equal(120m, q.TaxAmount);   // 600 × %20
        Assert.Equal(720m, q.Total);
        Assert.Equal(1, q.AccessMonths);
    }

    [Fact]
    public async Task BuildQuote_YıllıkPro_OnAyÖdenirOnİkiAyKullanılır()
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Yearly);

        // Fiyatlandırma sayfasındaki "2 ay hediye" vaadi: 600 × 10 = 6.000
        Assert.Equal(10, q!.BilledMonths);
        Assert.Equal(12, q.AccessMonths);
        Assert.Equal(6000m, q.Subtotal);
        Assert.Equal(7200m, q.Total);        // 6.000 + %20 KDV
        Assert.Equal(1200m, q.YearlySavings); // 2 aylık bedel
    }

    [Fact]
    public async Task BuildQuote_YıllıkUltra_PazarlamaSayfasıylaEşleşir()
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Ultra, BillingPeriods.Yearly);

        Assert.Equal(10000m, q!.Subtotal); // Pricing.cshtml: ₺10.000 / yıl
        Assert.Equal(12000m, q.Total);
    }

    [Fact]
    public async Task BuildQuote_KuponluAylıkPro_KdvİndirimSonrasıHesaplanır()
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Monthly, "WCAG25");

        Assert.Equal(600m, q!.Subtotal);
        Assert.Equal(150m, q.DiscountAmount); // %25
        Assert.Equal(450m, q.NetAmount);
        Assert.Equal(90m, q.TaxAmount);       // KDV net tutar üzerinden
        Assert.Equal(540m, q.Total);
    }

    [Fact]
    public async Task BuildQuote_GeçersizKupon_İndirimUygulanmaz()
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Monthly, "SAHTEKOD");

        Assert.Equal(0m, q!.DiscountAmount);
        Assert.Null(q.CouponCode);
        Assert.Equal(720m, q.Total);
    }

    [Fact]
    public async Task BuildQuote_YıllığaÖzelKuponAylıkta_Geçersiz()
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Monthly, "YILLIK15");

        Assert.Equal(0m, q!.DiscountAmount);
    }

    [Fact]
    public async Task BuildQuote_GeçersizDönem_Null()
    {
        Assert.Null(await _sut.BuildQuoteAsync(PlanIds.Pro, "Haftalık"));
    }

    [Fact]
    public async Task BuildQuote_KuponKüçükHarfle_NormalizeEdilir()
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Monthly, "  wcag25 ");

        Assert.Equal("WCAG25", q!.CouponCode);
        Assert.Equal(150m, q.DiscountAmount);
    }

    // ── Ülkeye göre KDV ────────────────────────────────────────────

    [Theory]
    [InlineData("TR", 120, 720)]   // %20 — Türkiye
    [InlineData("DE", 114, 714)]   // %19 — Almanya
    [InlineData("HU", 162, 762)]   // %27 — Macaristan
    [InlineData("US", 0, 600)]     // ulusal KDV yok
    public async Task BuildQuote_KdvFaturaUlkesineGoreHesaplanir(
        string countryCode, decimal expectedTax, decimal expectedTotal)
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Monthly, null, countryCode);

        Assert.Equal(600m, q!.Subtotal); // ara toplam ülkeden bağımsız
        Assert.Equal(expectedTax, q.TaxAmount);
        Assert.Equal(expectedTotal, q.Total);
        Assert.Equal(countryCode, q.CountryCode);
    }

    [Fact]
    public async Task BuildQuote_UlkeVerilmezse_VarsayilanTurkiyeOrani()
    {
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Monthly);

        Assert.Equal("TR", q!.CountryCode);
        Assert.Equal(0.20m, q.TaxRate);
    }

    [Fact]
    public async Task BuildQuote_KuponVeUlkeBirlikte_IndirimSonrasiUlkeOrani()
    {
        // Almanya %19: (600 − 150) × 0,19 = 85,50
        var q = await _sut.BuildQuoteAsync(PlanIds.Pro, BillingPeriods.Monthly, "WCAG25", "DE");

        Assert.Equal(150m, q!.DiscountAmount);
        Assert.Equal(450m, q.NetAmount);
        Assert.Equal(85.50m, q.TaxAmount);
        Assert.Equal(535.50m, q.Total);
    }

    // ── Fatura alanı doğrulaması (servis düzeyi) ───────────────────

    [Fact]
    public async Task CreateOrGetOrder_GecersizUlke_Reddedilir()
    {
        var command = NewCommand();
        command.BillingCountry = "Türkiye"; // ISO kodu değil, ad gönderilmiş

        var result = await _sut.CreateOrGetOrderAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Empty(_orders);
    }

    [Fact]
    public async Task CreateOrGetOrder_TurkiyeGecersizIl_Reddedilir()
    {
        var command = NewCommand();
        command.BillingCity = "Berlin";

        var result = await _sut.CreateOrGetOrderAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Contains("il", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateOrGetOrder_GecersizTelefon_Reddedilir()
    {
        var command = NewCommand();
        command.BillingPhone = "555 12";

        var result = await _sut.CreateOrGetOrderAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Empty(_orders);
    }

    [Fact]
    public async Task CreateOrGetOrder_GecersizEposta_Reddedilir()
    {
        var command = NewCommand();
        command.BillingEmail = "ada.example.com"; // @ yok

        var result = await _sut.CreateOrGetOrderAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Empty(_orders);
    }

    [Fact]
    public async Task CreateOrGetOrder_TelefonVeEposta_NormalizeEdilerekSaklanir()
    {
        var command = NewCommand();
        command.BillingPhone = "0555 123 45 67";
        command.BillingEmail = "  Ada@Example.COM ";

        var result = await _sut.CreateOrGetOrderAsync(command);

        Assert.Equal("+905551234567", result.Order!.BillingPhone);
        Assert.Equal("ada@example.com", result.Order.BillingEmail);
        Assert.Equal("TR", result.Order.BillingCountry);
    }

    [Fact]
    public async Task CreateOrGetOrder_YabanciUlke_KdvOUlkeninOraniyleYazilir()
    {
        var command = NewCommand();
        command.BillingCountry = "DE";
        command.BillingCity = "Berlin";       // Türkiye dışında serbest metin
        command.BillingPhone = "030 1234567";

        var result = await _sut.CreateOrGetOrderAsync(command);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(0.19m, result.Order!.TaxRate);
        Assert.Equal(114m, result.Order.TaxAmount);
        Assert.Equal(714m, result.Order.TotalAmount);
    }

    // ── Idempotency / çift gönderim ────────────────────────────────

    [Fact]
    public async Task CreateOrGetOrder_AynıJetonlaİkiKez_TekSiparişOluşur()
    {
        var command = NewCommand();

        var first = await _sut.CreateOrGetOrderAsync(command);
        var second = await _sut.CreateOrGetOrderAsync(NewCommand());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(second.WasExisting);
        Assert.Single(_orders);
        Assert.Equal(first.Order!.OrderReference, second.Order!.OrderReference);
    }

    [Fact]
    public async Task CreateOrGetOrder_FarklıKullanıcıAynıJeton_AyrıSiparişler()
    {
        // Idempotency anahtarı kullanıcı kimliğini de içerir; başkasının
        // jetonunu ele geçiren biri onun siparişine ulaşamaz.
        await _sut.CreateOrGetOrderAsync(NewCommand(userId: "u1"));
        await _sut.CreateOrGetOrderAsync(NewCommand(userId: "u2"));

        Assert.Equal(2, _orders.Count);
    }

    [Fact]
    public async Task CreateOrGetOrder_ÖdenmişSipariş_AlreadyPaidİşaretlenir()
    {
        var created = await _sut.CreateOrGetOrderAsync(NewCommand());
        created.Order!.Status = PaymentOrderStatus.Paid;

        var again = await _sut.CreateOrGetOrderAsync(NewCommand());

        Assert.True(again.AlreadyPaid);
        Assert.Single(_orders);
    }

    [Fact]
    public async Task CreateOrGetOrder_İstemciTutarGönderemez_TutarPlandanHesaplanır()
    {
        // CreateOrderCommand'da tutar alanı YOKTUR; sipariş tutarı yalnızca
        // PlanId'den gelir. Bu test o sözleşmeyi kilitler.
        var result = await _sut.CreateOrGetOrderAsync(NewCommand(planId: PlanIds.Ultra));

        Assert.Equal(1000m, result.Order!.Subtotal);
        Assert.Equal(1200m, result.Order.TotalAmount);
    }

    [Fact]
    public async Task CreateOrGetOrder_SatınAlınamayanPlan_Reddedilir()
    {
        var result = await _sut.CreateOrGetOrderAsync(NewCommand(planId: PlanIds.Enterprise));

        Assert.False(result.IsSuccess);
        Assert.Empty(_orders);
    }

    [Fact]
    public async Task CreateOrGetOrder_KurumsalFaturaKapalı_ŞirketAlanlarıTemizlenir()
    {
        var command = NewCommand();
        command.IsCompany = false;
        command.CompanyName = "Sızmamalı A.Ş.";
        command.TaxNumber = "1234567890";

        var result = await _sut.CreateOrGetOrderAsync(command);

        Assert.Null(result.Order!.CompanyName);
        Assert.Null(result.Order.TaxNumber);
    }

    // ── Tamamlama ──────────────────────────────────────────────────

    [Fact]
    public async Task CompleteOrder_TutarUyuşmuyor_AbonelikAçılmaz()
    {
        var order = (await _sut.CreateOrGetOrderAsync(NewCommand())).Order!;
        order.Status = PaymentOrderStatus.Pending;

        // Sağlayıcı eksik tutar bildirdi — kabul edilmemeli.
        var result = await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX1", 1m));

        Assert.False(result.Completed);
        Assert.NotEqual(PaymentOrderStatus.Paid, order.Status);
        _subRepo.Verify(r => r.AddAsync(It.IsAny<UserSubscription>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrder_ZatenÖdenmiş_TekrarİşlenmezVeAbonelikÇoğalmaz()
    {
        var order = (await _sut.CreateOrGetOrderAsync(NewCommand())).Order!;
        order.Status = PaymentOrderStatus.Paid;

        var result = await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX1", order.TotalAmount));

        Assert.True(result.Completed);
        Assert.True(result.WasAlreadyCompleted);
        _subRepo.Verify(r => r.AddAsync(It.IsAny<UserSubscription>()), Times.Never);
        _invoiceRepo.Verify(r => r.AddAsync(It.IsAny<Invoice>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrder_BaşarısızSipariş_YenidenTamamlanamaz()
    {
        var order = (await _sut.CreateOrGetOrderAsync(NewCommand())).Order!;
        order.Status = PaymentOrderStatus.Failed;

        var result = await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX1", order.TotalAmount));

        Assert.False(result.Completed);
        _subRepo.Verify(r => r.AddAsync(It.IsAny<UserSubscription>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrder_YıllıkÖdeme_AbonelikOnİkiAyGeçerli()
    {
        var order = (await _sut.CreateOrGetOrderAsync(
            NewCommand(period: BillingPeriods.Yearly))).Order!;
        order.Status = PaymentOrderStatus.Pending;

        UserSubscription? captured = null;
        _subRepo.Setup(r => r.AddAsync(It.IsAny<UserSubscription>()))
                .Callback((UserSubscription s) => captured = s)
                .Returns(Task.CompletedTask);

        await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX1", order.TotalAmount));

        Assert.NotNull(captured);
        // 10 ay ödendi ama 12 ay erişim verilir
        var months = (captured!.EndDate.Year - captured.StartDate.Year) * 12
                     + captured.EndDate.Month - captured.StartDate.Month;
        Assert.Equal(12, months);
        Assert.Equal(PaymentOrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task CompleteOrder_HamSağlayıcıYanıtıSaklanmaz()
    {
        var order = (await _sut.CreateOrGetOrderAsync(NewCommand())).Order!;
        order.Status = PaymentOrderStatus.Pending;
        order.PaymentProvider = "Sandbox";

        Payment? captured = null;
        _paymentRepo.Setup(r => r.AddAsync(It.IsAny<Payment>()))
                    .Callback((Payment p) => captured = p)
                    .Returns(Task.CompletedTask);

        await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX-ABC", order.TotalAmount));

        Assert.NotNull(captured);
        // Yalnızca kendi ürettiğimiz özet — sağlayıcı gövdesi değil.
        Assert.Contains("TX-ABC", captured!.RawResponse);
        Assert.Contains(order.OrderReference, captured.RawResponse);
        Assert.Equal(PaymentStatuses.Succeeded, captured.PaymentStatus);
    }

    [Fact]
    public async Task CompleteOrder_UcretsizAbonelikVarsa_Kapatilir()
    {
        // Kayıt akışı, deneme bitiminde devreye girsin diye ileri tarihli bir
        // Ücretsiz abonelik oluşturur. Ücretli plana geçildiğinde kapanmalı.
        var freeSub = new UserSubscription
        {
            Id = 99, UserId = "u1", PlanId = PlanIds.Free,
            IsActive = true, IsDeleted = false,
            StartDate = DateTime.UtcNow.AddDays(14), EndDate = DateTime.UtcNow.AddYears(1),
        };

        _subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync((Expression<Func<UserSubscription, bool>> p) =>
                    new[] { freeSub }.Where(p.Compile()).ToList());

        var order = (await _sut.CreateOrGetOrderAsync(NewCommand())).Order!;
        order.Status = PaymentOrderStatus.Pending;

        await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX1", order.TotalAmount));

        Assert.False(freeSub.IsActive);
        Assert.True(freeSub.IsDeleted);
        Assert.NotNull(freeSub.CanceledAt);
    }

    [Fact]
    public async Task CompleteOrder_PlanaGecen_UcretsizDenemesiKalkar()
    {
        // Bir plana geçen kullanıcıda ücretsiz deneme sona erer: deneme ekranı
        // bir daha gösterilmez, "denemeniz bitiyor" e-postası gönderilmez.
        var buyer = new AppUser
        {
            Id = "u1",
            TrialStartedAt = DateTime.UtcNow.AddDays(-2),
            TrialEndsAt = DateTime.UtcNow.AddDays(12),
        };
        _users.Add(buyer);

        var order = (await _sut.CreateOrGetOrderAsync(NewCommand())).Order!;
        order.Status = PaymentOrderStatus.Pending;

        await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX1", order.TotalAmount));

        Assert.False(buyer.IsTrialActive);
        Assert.True(buyer.TrialEndsAt <= DateTime.UtcNow);
        _userRepo.Verify(r => r.Update(buyer), Times.Once);
    }

    [Fact]
    public async Task CompleteOrder_DenemesiOlmayanKullanici_KullaniciSatiriYazilmaz()
    {
        _users.Add(new AppUser { Id = "u1", TrialEndsAt = null });

        var order = (await _sut.CreateOrGetOrderAsync(NewCommand())).Order!;
        order.Status = PaymentOrderStatus.Pending;

        await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX1", order.TotalAmount));

        Assert.Equal(PaymentOrderStatus.Paid, order.Status);
        _userRepo.Verify(r => r.Update(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task CompleteOrder_UcretsizAbonelikYoksa_HataVermez()
    {
        var order = (await _sut.CreateOrGetOrderAsync(NewCommand())).Order!;
        order.Status = PaymentOrderStatus.Pending;

        var result = await _sut.CompleteOrderAsync(order, PaymentResult.Success("TX1", order.TotalAmount));

        Assert.True(result.Completed);
    }

    // ── Sipariş erişimi ────────────────────────────────────────────

    [Fact]
    public async Task GetOrder_BaşkaKullanıcınınSiparişi_Null()
    {
        var order = (await _sut.CreateOrGetOrderAsync(NewCommand(userId: "u1"))).Order!;

        Assert.Null(await _sut.GetOrderAsync(order.OrderReference, "saldirgan"));
        Assert.NotNull(await _sut.GetOrderAsync(order.OrderReference, "u1"));
    }

    // ── Yardımcı ───────────────────────────────────────────────────

    private static CreateOrderCommand NewCommand(
        string userId = "u1",
        int planId = PlanIds.Pro,
        string period = BillingPeriods.Monthly,
        string clientToken = "token-1") => new()
        {
            UserId = userId,
            PlanId = planId,
            BillingPeriod = period,
            ClientToken = clientToken,
            BillingFullName = "Ada Lovelace",
            BillingEmail = "ada@example.com",
            BillingPhone = "+90 555 000 00 00",
            BillingCountry = "TR",       // ISO 3166-1 alpha-2 — KDV oranı buna bağlı
            BillingCity = "İstanbul",    // Türkiye'de 81 il listesinden gelmeli
            BillingAddress = "Örnek Mahallesi, Test Sokak No 1",
        };
}

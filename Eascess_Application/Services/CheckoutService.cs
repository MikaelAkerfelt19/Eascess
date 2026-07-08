using System.Security.Cryptography;
using System.Text;
using Eascess_Application.DTOs.Payments;
using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

/// <summary>
/// Ödeme akışının sunucu tarafı.
///
/// GÜVENLİK KURALI: Bu sınıfa istemciden yalnızca PlanId, dönem ve kupon KODU
/// girer. Tüm tutarlar Plans tablosundaki fiyattan yeniden hesaplanır; istemciden
/// gelen bir tutar hiçbir yolla siparişe yazılmaz.
/// </summary>
public class CheckoutService : ICheckoutService
{
    private readonly IRepository<Plan> _planRepo;
    private readonly IRepository<PaymentOrder> _orderRepo;
    private readonly IRepository<UserSubscription> _subscriptionRepo;
    private readonly IRepository<Payment> _paymentRepo;
    private readonly IRepository<Invoice> _invoiceRepo;
    private readonly IRepository<AppUser> _userRepo;
    private readonly ICouponService _couponService;
    private readonly IUnitOfWork _unitOfWork;

    public CheckoutService(
        IRepository<Plan> planRepo,
        IRepository<PaymentOrder> orderRepo,
        IRepository<UserSubscription> subscriptionRepo,
        IRepository<Payment> paymentRepo,
        IRepository<Invoice> invoiceRepo,
        IRepository<AppUser> userRepo,
        ICouponService couponService,
        IUnitOfWork unitOfWork)
    {
        _planRepo = planRepo;
        _orderRepo = orderRepo;
        _subscriptionRepo = subscriptionRepo;
        _paymentRepo = paymentRepo;
        _invoiceRepo = invoiceRepo;
        _userRepo = userRepo;
        _couponService = couponService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Plan?> GetPurchasablePlanAsync(int planId)
    {
        var plan = await _planRepo.FirstOrDefaultAsync(p => p.Id == planId);

        // Fiyatı 0 olan planlar satın alınamaz: Ücretsiz (zaten bedava) ve
        // Kurumsal (teklif usulü — satış ekibi üzerinden ilerler).
        if (plan is null || !plan.IsActive || plan.IsDeleted || plan.MonthlyPrice <= 0)
            return null;

        return plan;
    }

    public async Task<CheckoutQuote?> BuildQuoteAsync(
        int planId, string billingPeriod, string? couponCode = null, string? countryCode = null)
    {
        if (!BillingPeriods.IsValid(billingPeriod))
            return null;

        var plan = await GetPurchasablePlanAsync(planId);
        if (plan is null)
            return null;

        return BuildQuote(plan, billingPeriod, couponCode, countryCode);
    }

    public async Task<CouponValidationResult> ValidateCouponAsync(int planId, string billingPeriod, string? couponCode)
    {
        if (!BillingPeriods.IsValid(billingPeriod))
            return CouponValidationResult.Invalid("Geçersiz faturalama dönemi.");

        var plan = await GetPurchasablePlanAsync(planId);
        if (plan is null)
            return CouponValidationResult.Invalid("Geçersiz plan.");

        var billedMonths = BillingPolicy.BilledMonths(billingPeriod);
        var subtotal = BillingPolicy.RoundMoney(plan.MonthlyPrice * billedMonths);

        return _couponService.Validate(couponCode, subtotal, plan, billingPeriod);
    }

    public async Task<CheckoutOrderResult> CreateOrGetOrderAsync(CreateOrderCommand command)
    {
        if (!BillingPeriods.IsValid(command.BillingPeriod))
            return CheckoutOrderResult.Fail("Geçersiz faturalama dönemi.");

        var plan = await GetPurchasablePlanAsync(command.PlanId);
        if (plan is null)
            return CheckoutOrderResult.Fail("Seçilen plan satın alınamıyor.");

        // Ülke KDV oranını belirlediği için burada da doğrulanır — controller
        // doğrulamasını atlayan bir çağrı yanlış oranla sipariş üretemez.
        var country = Countries.Find(command.BillingCountry);
        if (country is null)
            return CheckoutOrderResult.Fail("Geçersiz ülke seçimi.");

        // Türkiye'de şehir 81 il listesinden gelmelidir.
        if (country.Code == Countries.DefaultCode && !TurkeyProvinces.IsValid(command.BillingCity))
            return CheckoutOrderResult.Fail("Lütfen listeden geçerli bir il seçin.");

        var phone = BillingContactRules.ValidatePhone(command.BillingPhone, country.Code);
        if (!phone.IsValid)
            return CheckoutOrderResult.Fail(phone.ErrorMessage!);

        if (!BillingContactRules.IsValidEmail(command.BillingEmail))
            return CheckoutOrderResult.Fail("Geçerli bir e-posta adresi girin.");

        var idempotencyKey = BuildIdempotencyKey(command.UserId, command.ClientToken);

        // Çift gönderim koruması: aynı anahtarla gelen ikinci istek yeni sipariş
        // oluşturmaz. Ödenmişse kullanıcı doğrudan başarı sayfasına gider;
        // beklemedeyse aynı sipariş yeniden kullanılır.
        var existing = await _orderRepo.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
        if (existing is not null)
        {
            return new CheckoutOrderResult
            {
                Order = existing,
                WasExisting = true,
                AlreadyPaid = existing.Status == PaymentOrderStatus.Paid,
            };
        }

        var quote = BuildQuote(plan, command.BillingPeriod, command.CouponCode, country.Code);

        var order = new PaymentOrder
        {
            OrderReference = GenerateOrderReference(),
            IdempotencyKey = idempotencyKey,
            UserId = command.UserId,
            PlanId = plan.Id,
            BillingPeriod = command.BillingPeriod,
            Currency = quote.Currency,

            UnitPrice = quote.UnitPrice,
            BilledMonths = quote.BilledMonths,
            Subtotal = quote.Subtotal,
            CouponCode = quote.CouponCode,
            DiscountAmount = quote.DiscountAmount,
            TaxRate = quote.TaxRate,
            TaxAmount = quote.TaxAmount,
            TotalAmount = quote.Total,

            BillingFullName = Trim(command.BillingFullName, 200),
            // Normalize edilmiş biçimde saklanır: e-posta küçük harf,
            // telefon "+<ülkekodu><ulusal>" — fatura ve sağlayıcı çağrısı tutarlı olur.
            BillingEmail = Trim(BillingContactRules.NormalizeEmail(command.BillingEmail), 256),
            BillingPhone = Trim(phone.Normalized, 40),
            // Ülke ISO kodu olarak saklanır; görünen ad Countries.NameFor ile türetilir.
            BillingCountry = country.Code,
            BillingCity = Trim(command.BillingCity, 100),
            BillingAddress = Trim(command.BillingAddress, 500),

            IsCompany = command.IsCompany,
            CompanyName = command.IsCompany ? Trim(command.CompanyName, 200) : null,
            TaxOffice = command.IsCompany ? Trim(command.TaxOffice, 100) : null,
            TaxNumber = command.IsCompany ? Trim(command.TaxNumber, 50) : null,

            Status = PaymentOrderStatus.Draft,
            CreatedAt = DateTime.UtcNow,
        };

        await _orderRepo.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return new CheckoutOrderResult { Order = order };
    }

    public async Task<PaymentOrder?> GetOrderAsync(string orderReference, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(orderReference))
            return null;

        var order = await _orderRepo.FirstOrDefaultAsync(o => o.OrderReference == orderReference);
        if (order is null)
            return null;

        // Sahiplik kontrolü: başkasının sipariş numarasını bilen biri onu göremez.
        if (userId is not null && order.UserId != userId)
            return null;

        return order;
    }

    public async Task MarkPendingAsync(PaymentOrder order, string providerName, string? providerTransactionId)
    {
        if (PaymentOrderStatus.IsTerminal(order.Status))
            return;

        order.Status = PaymentOrderStatus.Pending;
        order.PaymentProvider = providerName;
        if (!string.IsNullOrWhiteSpace(providerTransactionId))
            order.ProviderTransactionId = providerTransactionId;

        _orderRepo.Update(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<OrderCompletionResult> CompleteOrderAsync(PaymentOrder order, PaymentResult result)
    {
        // Tekrarlanan callback koruması: sağlayıcılar aynı bildirimi birden çok
        // kez gönderebilir. Terminal durumdaki sipariş bir daha işlenmez —
        // ikinci bir abonelik veya fatura oluşmaz.
        if (order.Status == PaymentOrderStatus.Paid)
            return new OrderCompletionResult { Completed = true, WasAlreadyCompleted = true };

        if (PaymentOrderStatus.IsTerminal(order.Status))
            return new OrderCompletionResult { Completed = false, ErrorMessage = "Sipariş kapatılmış." };

        // Sağlayıcı bir tutar bildirdiyse siparişteki tutarla eşleşmek ZORUNDA.
        // Eşleşmiyorsa ödeme kabul edilmez — eksik tahsilatla abonelik açılmaz.
        if (result.PaidAmount.HasValue &&
            BillingPolicy.RoundMoney(result.PaidAmount.Value) != BillingPolicy.RoundMoney(order.TotalAmount))
        {
            return new OrderCompletionResult
            {
                Completed = false,
                ErrorMessage = "Ödenen tutar sipariş tutarıyla eşleşmiyor.",
            };
        }

        var now = DateTime.UtcNow;
        var accessMonths = BillingPolicy.AccessMonths(order.BillingPeriod);

        // Abonelik: yeni bir kayıt açılır. PlanService.GetUserActivePlanAsync
        // o an geçerli abonelikler içinden en yüksek kademeyi seçtiği için
        // yükseltme anında devreye girer, mevcut kayıtlara dokunmak gerekmez.
        var subscription = new UserSubscription
        {
            UserId = order.UserId,
            PlanId = order.PlanId,
            StartDate = now,
            EndDate = now.AddMonths(accessMonths),
            AutoRenew = true,
            IsActive = true,
            IsDeleted = false,
            PaymentProviderSubscriptionId = result.ProviderTransactionId,
        };

        await _subscriptionRepo.AddAsync(subscription);

        // Ücretli plana geçildiğinde duran Ücretsiz abonelik kapatılır —
        // kullanıcı aynı anda iki planda görünmez ve abonelik listesi temiz kalır.
        // (Kayıt akışı, deneme bitiminde devreye girsin diye ileri tarihli bir
        // Ücretsiz satır oluşturur; artık gereksizdir.)
        // Not: ücretli abonelik sona erdiğinde PlanService zaten Ücretsiz'e
        // düşer — satır silmek kullanıcıyı plansız bırakmaz.
        var freeSubs = await _subscriptionRepo.FindAsync(
            s => s.UserId == order.UserId && s.PlanId == PlanIds.Free && !s.IsDeleted);

        foreach (var free in freeSubs)
        {
            free.IsActive = false;
            free.IsDeleted = true;
            free.CanceledAt = now;
            _subscriptionRepo.Update(free);
        }

        // Bir plana geçen kullanıcının ücretsiz denemesi burada kalkar: deneme
        // bitişi "şimdi"ye çekilir. Böylece deneme ekranı bir daha görünmez ve
        // "denemeniz bitiyor" hatırlatma e-postası gönderilmez.
        // (Kullanıcı satırı bulunamazsa ödeme akışı bozulmaz — deneme gösterimi
        // ayrıca aktif plan kademesine bakılarak da kapatılır.)
        var buyer = await _userRepo.FirstOrDefaultAsync(u => u.Id == order.UserId);
        if (buyer is not null && buyer.TrialEndsAt.HasValue && buyer.TrialEndsAt.Value > now)
        {
            buyer.TrialEndsAt = now;
            _userRepo.Update(buyer);
        }

        await _unitOfWork.SaveChangesAsync();

        var payment = new Payment
        {
            UserId = order.UserId,
            SubscriptionId = subscription.Id,
            Amount = order.TotalAmount,
            Currency = order.Currency,
            PaymentDate = now,
            PaymentProvider = order.PaymentProvider,
            PaymentStatus = PaymentStatuses.Succeeded,
            TransactionId = result.ProviderTransactionId,
            // Sağlayıcının HAM yanıtı saklanmaz — kart verisi taşıyabilir.
            // Mutabakat için yalnızca kendi ürettiğimiz özet yazılır.
            RawResponse = BuildSanitizedSummary(order, result),
        };

        await _paymentRepo.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        var invoice = new Invoice
        {
            UserId = order.UserId,
            PaymentId = payment.Id,
            Amount = order.TotalAmount,
            Currency = order.Currency,
            IsPaid = true,
            CreatedAt = now,
            PaidAt = now,
        };

        await _invoiceRepo.AddAsync(invoice);

        order.Status = PaymentOrderStatus.Paid;
        order.PaymentId = payment.Id;
        order.CompletedAt = now;
        if (!string.IsNullOrWhiteSpace(result.ProviderTransactionId))
            order.ProviderTransactionId = result.ProviderTransactionId;

        _orderRepo.Update(order);
        await _unitOfWork.SaveChangesAsync();

        return new OrderCompletionResult { Completed = true };
    }

    public async Task FailOrderAsync(PaymentOrder order, string? errorCode, string? errorMessage)
    {
        if (PaymentOrderStatus.IsTerminal(order.Status))
            return;

        order.Status = PaymentOrderStatus.Failed;
        order.ErrorCode = Trim(errorCode, 50);
        order.ErrorMessage = Trim(errorMessage, 500);
        order.CompletedAt = DateTime.UtcNow;

        _orderRepo.Update(order);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── Yardımcılar ────────────────────────────────────────────────

    /// <summary>
    /// Fiyat hesabının TEK noktası. Sıra önemlidir:
    /// ara toplam → indirim → net → KDV → toplam.
    /// KDV indirim düşüldükten SONRAKİ tutar üzerinden hesaplanır.
    /// </summary>
    private CheckoutQuote BuildQuote(Plan plan, string billingPeriod, string? couponCode, string? countryCode)
    {
        var billedMonths = BillingPolicy.BilledMonths(billingPeriod);
        var accessMonths = BillingPolicy.AccessMonths(billingPeriod);

        var subtotal = BillingPolicy.RoundMoney(plan.MonthlyPrice * billedMonths);

        var coupon = _couponService.Validate(couponCode, subtotal, plan, billingPeriod);
        var discount = coupon.IsValid ? coupon.DiscountAmount : 0m;

        // KDV oranı fatura ülkesine göre belirlenir; bilinmeyen kodda
        // varsayılan ülkenin (Türkiye) oranı uygulanır.
        var country = Countries.Find(countryCode) ?? Countries.Default;

        var net = BillingPolicy.RoundMoney(subtotal - discount);
        var tax = BillingPolicy.RoundMoney(net * country.VatRate);
        var total = BillingPolicy.RoundMoney(net + tax);

        // Yıllıkta 12 ay kullanılır, 10 ay ödenir — kazanç 2 aylık bedeldir.
        var savings = billingPeriod == BillingPeriods.Yearly
            ? BillingPolicy.RoundMoney(plan.MonthlyPrice * (accessMonths - billedMonths))
            : 0m;

        return new CheckoutQuote
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            BillingPeriod = billingPeriod,
            Currency = BillingPolicy.Currency,
            UnitPrice = plan.MonthlyPrice,
            BilledMonths = billedMonths,
            AccessMonths = accessMonths,
            Subtotal = subtotal,
            CouponCode = coupon.IsValid ? coupon.NormalizedCode : null,
            DiscountAmount = discount,
            DiscountLabel = coupon.IsValid ? coupon.Label : null,
            NetAmount = net,
            CountryCode = country.Code,
            CountryName = country.Name,
            TaxRate = country.VatRate,
            TaxAmount = tax,
            Total = total,
            YearlySavings = savings,
        };
    }

    /// <summary>
    /// Idempotency anahtarı: kullanıcı + form jetonu. Kullanıcı kimliği karışıma
    /// dahildir, böylece başkasının jetonunu ele geçiren biri onun siparişine
    /// erişemez. SHA-256 hex tam 64 karakterdir — sütun sınırına uyar.
    /// </summary>
    private static string BuildIdempotencyKey(string userId, string clientToken)
    {
        var material = $"{userId}|{clientToken}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateOrderReference() =>
        $"EA-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    /// <summary>Yalnızca kendi ürettiğimiz alanlar — sağlayıcı ham verisi içermez.</summary>
    private static string BuildSanitizedSummary(PaymentOrder order, PaymentResult result) =>
        $"{{\"provider\":\"{order.PaymentProvider}\"," +
        $"\"orderReference\":\"{order.OrderReference}\"," +
        $"\"transactionId\":\"{result.ProviderTransactionId}\"," +
        $"\"amount\":{order.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
        $"\"currency\":\"{order.Currency}\"}}";

    private static string Trim(string? value, int maxLength)
    {
        var trimmed = (value ?? "").Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

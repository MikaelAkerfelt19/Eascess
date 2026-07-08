using Eascess.Models;
using Eascess_Application.DTOs.Payments;
using Eascess_Application.Options;
using Eascess_Application.Services;
using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Eascess.Controllers;

/// <summary>
/// Plan seçiminden sonraki ödeme akışı.
///
/// Akış:
///   GET  /Checkout?planId=&period=   → fatura formu + sunucuda hesaplanan özet
///   POST /Checkout/Start             → sipariş oluştur, sağlayıcıya yönlendir
///   GET  /Checkout/Sandbox           → sahte sağlayıcının ödeme ekranı (yalnızca Sandbox)
///   ANY  /Checkout/Callback          → sağlayıcının geri dönüşü (imza doğrulanır)
///   GET  /Checkout/Success|Failure   → sonuç sayfaları
///
/// GÜVENLİK: Fiyat hiçbir adımda istemciden alınmaz; her seferinde PlanId
/// üzerinden CheckoutService'te yeniden hesaplanır.
/// </summary>
[Authorize]
public class CheckoutController : Controller
{
    private readonly ICheckoutService _checkoutService;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IPlanService _planService;
    private readonly UserManager<AppUser> _userManager;
    private readonly PaymentOptions _paymentOptions;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICheckoutService checkoutService,
        IPaymentProvider paymentProvider,
        IPlanService planService,
        UserManager<AppUser> userManager,
        IOptions<PaymentOptions> paymentOptions,
        ILogger<CheckoutController> logger)
    {
        _checkoutService = checkoutService;
        _paymentProvider = paymentProvider;
        _planService = planService;
        _userManager = userManager;
        _paymentOptions = paymentOptions.Value;
        _logger = logger;
    }

    // GET /Checkout?planId=2&period=Monthly
    [HttpGet]
    public async Task<IActionResult> Index(int planId, string period = BillingPeriods.Monthly, string? coupon = null)
    {
        if (!BillingPeriods.IsValid(period))
            period = BillingPeriods.Monthly;

        var plan = await _checkoutService.GetPurchasablePlanAsync(planId);
        if (plan is null)
        {
            // Ücretsiz ve teklif usulü planlar bu ekrandan geçmez.
            TempData["PlanWarning"] = "Seçtiğiniz plan çevrimiçi satın alınamıyor. Lütfen bir plan seçin.";
            return RedirectToAction("Index", "Subscription");
        }

        // Ekran ilk açılışta varsayılan ülkenin (Türkiye) KDV oranıyla çizilir;
        // kullanıcı ülkeyi değiştirdiğinde özet sunucuda yeniden hesaplanır.
        var quote = await _checkoutService.BuildQuoteAsync(planId, period, coupon, Countries.DefaultCode);
        if (quote is null)
            return RedirectToAction("Index", "Subscription");

        var user = await _userManager.GetUserAsync(User);
        var currentPlan = await _planService.GetUserActivePlanAsync(_userManager.GetUserId(User)!);

        var model = new CheckoutViewModel
        {
            Plan = plan,
            Quote = quote,
            CurrentPlanName = currentPlan.Name,
            Form = new CheckoutFormModel
            {
                PlanId = plan.Id,
                BillingPeriod = period,
                CouponCode = quote.CouponCode,
                ClientToken = Guid.NewGuid().ToString("N"),
                // Bilinen bilgiler önden doldurulur; kullanıcı değiştirebilir.
                FullName = user?.FullName ?? "",
                Email = user?.Email ?? "",
                // Ülke kodu formda ayrı bir ön ek olarak gösterildiği için
                // kayıtlı numaranın yalnızca ulusal kısmı alana yazılır.
                Phone = NationalPhonePart(user?.PhoneNumber, Countries.DefaultCode),
                Country = Countries.DefaultCode,
            },
        };

        // Kupon denendi ama geçersizse kullanıcıya sebebini göster.
        if (!string.IsNullOrWhiteSpace(coupon) && quote.DiscountAmount == 0)
        {
            var validation = await _checkoutService.ValidateCouponAsync(planId, period, coupon);
            model.CouponError = validation.ErrorMessage ?? "Bu kod geçerli değil.";
            model.Form.CouponCode = coupon;
        }

        return View(model);
    }

    /// <summary>
    /// Sipariş özetini yeniden hesaplar — kupon "Uygula" ve dönem değişimi bunu çağırır.
    ///
    /// Hesap tamamen sunucudadır: istemci yalnızca plan, dönem ve kupon KODUNU
    /// gönderir; ekranda gösterilecek tutarları biçimlenmiş olarak geri alır.
    /// Böylece istemcide fiyat mantığı kopyalanmaz.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quote(
        [FromForm] int planId, [FromForm] string billingPeriod,
        [FromForm] string? couponCode, [FromForm] string? countryCode)
    {
        var validation = await _checkoutService.ValidateCouponAsync(planId, billingPeriod, couponCode);

        // Kupon geçersizse indirimsiz özet döner — ekran yine de güncel kalır.
        var quote = await _checkoutService.BuildQuoteAsync(
            planId, billingPeriod, validation.IsValid ? couponCode : null, countryCode);

        if (quote is null)
            return BadRequest(new { message = "Geçersiz plan veya faturalama dönemi." });

        var isYearly = quote.BillingPeriod == BillingPeriods.Yearly;

        return Json(new
        {
            couponValid = validation.IsValid,
            couponAttempted = !string.IsNullOrWhiteSpace(couponCode),
            couponMessage = validation.IsValid ? validation.Label : validation.ErrorMessage,
            couponCode = quote.CouponCode,
            hasDiscount = quote.DiscountAmount > 0,

            subtotal = Money.Format(quote.Subtotal, quote.Currency),
            discount = "−" + Money.Format(quote.DiscountAmount, quote.Currency),
            tax = Money.Format(quote.TaxAmount, quote.Currency),
            total = Money.Format(quote.Total, quote.Currency),

            // KDV satırının etiketi ülkeye göre değişir: oran ve ülke adı gösterilir.
            taxLabel = $"KDV (%{quote.TaxRate * 100:0.##}) · {quote.CountryName}",
            dialCode = Countries.DialCodeFor(quote.CountryCode),

            subtotalLabel = isYearly
                ? $"{quote.PlanName} · yıllık ({quote.BilledMonths} ay ödeme)"
                : $"{quote.PlanName} · aylık",
            discountLabel = quote.CouponCode is not null ? $"İndirim ({quote.CouponCode})" : "İndirim",
            submitLabel = $"{Money.Format(quote.Total, quote.Currency)} Öde",
            periodNote = isYearly
                ? "12 ay boyunca geçerlidir; 10 ay ücretlendirilir."
                : "1 ay boyunca geçerlidir, dönem sonunda yenilenir.",
            savingsNote = isYearly && quote.YearlySavings > 0
                ? $"Aylık ödemeye göre {Money.Format(quote.YearlySavings, quote.Currency)} tasarruf."
                : null,
        });
    }

    // POST /Checkout/Start
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(CheckoutFormModel form, string? intent = null)
    {
        var userId = _userManager.GetUserId(User)!;

        var plan = await _checkoutService.GetPurchasablePlanAsync(form.PlanId);
        if (plan is null)
        {
            TempData["PlanWarning"] = "Seçtiğiniz plan çevrimiçi satın alınamıyor.";
            return RedirectToAction("Index", "Subscription");
        }

        // JavaScript kapalıyken kupon/dönem güncellemesi buradan geçer: ödeme
        // başlatılmaz, sayfa yeni özetle yeniden çizilir. Form henüz
        // doldurulmadığı için doğrulama hataları gösterilmez.
        if (intent == "recalc")
        {
            ModelState.Clear();
            var recalculated = await BuildViewModelAsync(form, plan);

            if (!string.IsNullOrWhiteSpace(form.CouponCode) && recalculated.Quote.DiscountAmount == 0)
            {
                var validation = await _checkoutService.ValidateCouponAsync(
                    form.PlanId, form.BillingPeriod, form.CouponCode);
                recalculated.CouponError = validation.ErrorMessage ?? "Bu kod geçerli değil.";
            }

            return View(nameof(Index), recalculated);
        }

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildViewModelAsync(form, plan));

        // Jeton kaybolmuşsa (JS kapalı, form kopyalanmış) üretilir —
        // idempotency anahtarı olmadan çift gönderim koruması çalışmaz.
        if (string.IsNullOrWhiteSpace(form.ClientToken))
            form.ClientToken = Guid.NewGuid().ToString("N");

        var orderResult = await _checkoutService.CreateOrGetOrderAsync(new CreateOrderCommand
        {
            UserId = userId,
            PlanId = form.PlanId,
            BillingPeriod = form.BillingPeriod,
            CouponCode = form.CouponCode,
            ClientToken = form.ClientToken,
            BillingFullName = form.FullName,
            BillingEmail = form.Email,
            BillingPhone = form.Phone,
            BillingCountry = form.Country,
            BillingCity = form.City,
            BillingAddress = form.Address,
            IsCompany = form.IsCompany,
            CompanyName = form.CompanyName,
            TaxOffice = form.TaxOffice,
            TaxNumber = form.TaxNumber,
        });

        if (!orderResult.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, orderResult.ErrorMessage ?? "Sipariş oluşturulamadı.");
            return View(nameof(Index), await BuildViewModelAsync(form, plan));
        }

        var order = orderResult.Order!;

        // Çift gönderim: aynı jetonla gelen ikinci istek yeni tahsilat başlatmaz.
        if (orderResult.AlreadyPaid)
        {
            _logger.LogInformation(
                "[Ödeme] Ödenmiş sipariş yeniden gönderildi, tahsilat tekrarlanmadı. Sipariş={OrderReference}",
                order.OrderReference);
            return RedirectToAction(nameof(Success), new { orderRef = order.OrderReference });
        }

        // Ödeme girişimi kaydı. Kart verisi yoktur (hosted akış) ve loglanmaz.
        _logger.LogInformation(
            "[Ödeme] Girişim başlıyor. Sipariş={OrderReference} Kullanıcı={UserId} Plan={PlanId} Dönem={Period} Tutar={Amount} {Currency} Sağlayıcı={Provider}",
            order.OrderReference, userId, order.PlanId, order.BillingPeriod,
            order.TotalAmount, order.Currency, _paymentProvider.ProviderName);

        PaymentResult result;
        try
        {
            result = await _paymentProvider.CreatePaymentAsync(BuildPaymentRequest(order, plan), HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            // Ağ/sağlayıcı hatası: sipariş başarısız kapatılır, kullanıcıya genel
            // mesaj gösterilir. Ayrıntı yalnızca logda kalır.
            _logger.LogError(ex,
                "[Ödeme] Sağlayıcı çağrısı başarısız. Sipariş={OrderReference} Sağlayıcı={Provider}",
                order.OrderReference, _paymentProvider.ProviderName);

            await _checkoutService.FailOrderAsync(order, "provider_unreachable",
                "Ödeme sağlayıcısına ulaşılamadı. Lütfen birkaç dakika sonra tekrar deneyin.");

            return RedirectToAction(nameof(Failure), new { orderRef = order.OrderReference });
        }

        switch (result.Status)
        {
            case PaymentResultStatus.RedirectRequired:
                await _checkoutService.MarkPendingAsync(order, _paymentProvider.ProviderName, result.ProviderTransactionId);

                if (!string.IsNullOrWhiteSpace(result.RedirectUrl))
                    return Redirect(result.RedirectUrl);

                // 3DS formu gibi HTML içerik: tarayıcıda otomatik gönderilir.
                ViewData["ProviderHtml"] = result.HtmlContent;
                return View("Redirecting");

            case PaymentResultStatus.Pending:
                await _checkoutService.MarkPendingAsync(order, _paymentProvider.ProviderName, result.ProviderTransactionId);
                return View("Redirecting");

            case PaymentResultStatus.Succeeded:
                await _checkoutService.MarkPendingAsync(order, _paymentProvider.ProviderName, result.ProviderTransactionId);
                var immediate = await _checkoutService.CompleteOrderAsync(order, result);
                // Tanıtım penceresi yalnızca planın GERÇEKTEN değiştiği anda gösterilir.
                if (immediate.Completed && !immediate.WasAlreadyCompleted)
                    TempData["ShowPlanIntro"] = order.PlanId;
                return RedirectToAction(nameof(Success), new { orderRef = order.OrderReference });

            default:
                await _checkoutService.FailOrderAsync(order, result.ErrorCode, result.ErrorMessage);
                return RedirectToAction(nameof(Failure), new { orderRef = order.OrderReference });
        }
    }

    /// <summary>
    /// Sağlayıcının geri dönüşü.
    ///
    /// Anonimdir ve antiforgery jetonu aranmaz — istek dış bir sistemden gelir.
    /// Güvenlik, gövdenin İMZASININ doğrulanmasına dayanır; imza geçersizse
    /// istek hiçbir şekilde ödeme sayılmaz.
    /// </summary>
    [HttpGet]
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Callback()
    {
        var context = await BuildCallbackContextAsync();

        PaymentResult result;
        try
        {
            result = await _paymentProvider.VerifyCallbackAsync(context, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Ödeme] Callback doğrulaması hata verdi. Sağlayıcı={Provider}",
                _paymentProvider.ProviderName);
            return RedirectToAction(nameof(Failure));
        }

        if (result.Status == PaymentResultStatus.SignatureInvalid)
        {
            // Güvenlik olayı: doğrulanmamış callback. Sipariş DEĞİŞTİRİLMEZ.
            _logger.LogWarning(
                "[Ödeme] Callback imzası geçersiz — istek yok sayıldı. Sipariş={OrderReference} IP={Ip}",
                result.OrderReference, HttpContext.Connection.RemoteIpAddress?.ToString());
            return RedirectToAction(nameof(Failure));
        }

        if (string.IsNullOrWhiteSpace(result.OrderReference))
        {
            _logger.LogWarning("[Ödeme] Callback sipariş numarası içermiyor.");
            return RedirectToAction(nameof(Failure));
        }

        // Sahiplik kontrolü yapılmaz: callback kullanıcı oturumu olmadan da
        // gelebilir. Siparişi bulmak imza doğrulandıktan sonra güvenlidir.
        var order = await _checkoutService.GetOrderAsync(result.OrderReference);
        if (order is null)
        {
            _logger.LogWarning("[Ödeme] Callback'teki sipariş bulunamadı. Sipariş={OrderReference}",
                result.OrderReference);
            return RedirectToAction(nameof(Failure));
        }

        if (result.IsSuccess)
        {
            var completion = await _checkoutService.CompleteOrderAsync(order, result);

            if (!completion.Completed)
            {
                _logger.LogWarning(
                    "[Ödeme] Sipariş tamamlanamadı. Sipariş={OrderReference} Sebep={Reason}",
                    order.OrderReference, completion.ErrorMessage);

                await _checkoutService.FailOrderAsync(order, "completion_failed", completion.ErrorMessage);
                return RedirectToAction(nameof(Failure), new { orderRef = order.OrderReference });
            }

            _logger.LogInformation(
                "[Ödeme] Başarılı. Sipariş={OrderReference} Tutar={Amount} {Currency} Tekrar={Repeat}",
                order.OrderReference, order.TotalAmount, order.Currency, completion.WasAlreadyCompleted);

            // Tekrarlanan callback'te pencere yeniden açılmaz — yalnızca ilk
            // tamamlanmada, yani planın gerçekten değiştiği anda gösterilir.
            if (!completion.WasAlreadyCompleted)
                TempData["ShowPlanIntro"] = order.PlanId;

            return RedirectToAction(nameof(Success), new { orderRef = order.OrderReference });
        }

        _logger.LogInformation(
            "[Ödeme] Başarısız. Sipariş={OrderReference} Kod={ErrorCode}",
            order.OrderReference, result.ErrorCode);

        await _checkoutService.FailOrderAsync(order, result.ErrorCode, result.ErrorMessage);
        return RedirectToAction(nameof(Failure), new { orderRef = order.OrderReference });
    }

    // GET /Checkout/Success?orderRef=
    [HttpGet]
    public async Task<IActionResult> Success(string? orderRef)
    {
        var userId = _userManager.GetUserId(User)!;
        var order = orderRef is null ? null : await _checkoutService.GetOrderAsync(orderRef, userId);

        if (order is null || order.Status != PaymentOrderStatus.Paid)
            return RedirectToAction("Index", "Subscription");

        var plan = await _checkoutService.GetPurchasablePlanAsync(order.PlanId);

        return View(new CheckoutResultViewModel
        {
            OrderReference = order.OrderReference,
            PlanName = plan?.Name ?? "",
            BillingPeriodLabel = PeriodLabel(order.BillingPeriod),
            Total = order.TotalAmount,
            Currency = order.Currency,
            CompletedAt = order.CompletedAt,
            AccessUntil = order.CompletedAt?.AddMonths(BillingPolicy.AccessMonths(order.BillingPeriod)),
            PlanId = order.PlanId,
            BillingPeriod = order.BillingPeriod,
        });
    }

    // GET /Checkout/Failure?orderRef=
    [HttpGet]
    public async Task<IActionResult> Failure(string? orderRef)
    {
        var userId = _userManager.GetUserId(User)!;
        var order = orderRef is null ? null : await _checkoutService.GetOrderAsync(orderRef, userId);

        if (order is null)
        {
            // Sipariş bilinmiyorsa da hata sayfası gösterilir — kullanıcı
            // boşluğa düşmez, panele dönüş bağlantısı verilir.
            return View(new CheckoutResultViewModel
            {
                ErrorMessage = "Ödeme tamamlanamadı. Tutar hesabınızdan çekilmedi.",
            });
        }

        var plan = await _checkoutService.GetPurchasablePlanAsync(order.PlanId);

        return View(new CheckoutResultViewModel
        {
            OrderReference = order.OrderReference,
            PlanName = plan?.Name ?? "",
            BillingPeriodLabel = PeriodLabel(order.BillingPeriod),
            Total = order.TotalAmount,
            Currency = order.Currency,
            ErrorCode = order.ErrorCode,
            ErrorMessage = order.ErrorMessage ?? "Ödeme tamamlanamadı. Tutar hesabınızdan çekilmedi.",
            PlanId = order.PlanId,
            BillingPeriod = order.BillingPeriod,
        });
    }

    /// <summary>
    /// Sahte sağlayıcının barındırılan ödeme ekranı — gerçek sağlayıcının
    /// kendi sitesinde göstereceği sayfanın yerine geçer. Yalnızca Sandbox
    /// sağlayıcısı etkinken erişilebilir.
    /// </summary>
    [HttpGet]
    public IActionResult Sandbox(
        string orderReference, string transactionId, string amount, string currency,
        string successSignature, string failureSignature)
    {
        if (_paymentProvider.ProviderName != "Sandbox")
            return NotFound();

        ViewData["OrderReference"] = orderReference;
        ViewData["TransactionId"] = transactionId;
        ViewData["Amount"] = amount;
        ViewData["Currency"] = currency;
        ViewData["SuccessSignature"] = successSignature;
        ViewData["FailureSignature"] = failureSignature;
        ViewData["CallbackUrl"] = ResolveCallbackUrl();

        return View();
    }

    // ── Yardımcılar ────────────────────────────────────────────────

    private PaymentRequest BuildPaymentRequest(PaymentOrder order, Plan plan)
    {
        var items = new List<PaymentBasketItem>
        {
            new()
            {
                Id = $"plan-{plan.Id}-{order.BillingPeriod}",
                Name = $"{plan.Name} — {PeriodLabel(order.BillingPeriod)}",
                Category = "Abonelik",
                Price = order.Subtotal,
            },
        };

        // İndirim ayrı kalem olarak gider; kalemler toplamı net tutara eşit kalır.
        if (order.DiscountAmount > 0)
        {
            items.Add(new PaymentBasketItem
            {
                Id = $"coupon-{order.CouponCode}",
                Name = $"İndirim ({order.CouponCode})",
                Category = "İndirim",
                Price = -order.DiscountAmount,
            });
        }

        return new PaymentRequest
        {
            OrderReference = order.OrderReference,
            IdempotencyKey = order.IdempotencyKey,
            Amount = order.TotalAmount,
            NetAmount = order.Subtotal - order.DiscountAmount,
            TaxAmount = order.TaxAmount,
            Currency = order.Currency,
            PlanId = order.PlanId,
            PlanName = plan.Name,
            BillingPeriod = order.BillingPeriod,
            CallbackUrl = ResolveCallbackUrl(),
            BuyerIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Buyer = new PaymentBuyer
            {
                UserId = order.UserId,
                FullName = order.BillingFullName,
                Email = order.BillingEmail,
                Phone = order.BillingPhone,
                IsCompany = order.IsCompany,
                CompanyName = order.CompanyName,
                TaxOffice = order.TaxOffice,
                TaxNumber = order.TaxNumber,
            },
            BillingAddress = new PaymentBillingAddress
            {
                ContactName = order.BillingFullName,
                // Sağlayıcılar ülkenin okunabilir adını bekler; siparişte ISO kodu saklanır.
                Country = Countries.NameFor(order.BillingCountry),
                City = order.BillingCity,
                Address = order.BillingAddress,
            },
            BasketItems = items,
        };
    }

    /// <summary>
    /// Callback adresi: yapılandırmada tanımlıysa o kullanılır. Üretimde
    /// açıkça ayarlanmalıdır — ters vekil arkasında istek host'u yanıltıcı olabilir.
    /// </summary>
    private string ResolveCallbackUrl()
    {
        if (!string.IsNullOrWhiteSpace(_paymentOptions.CallbackUrl))
            return _paymentOptions.CallbackUrl;

        return $"{Request.Scheme}://{Request.Host}{Url.Action(nameof(Callback), "Checkout")}";
    }

    private async Task<PaymentCallbackContext> BuildCallbackContextAsync()
    {
        string rawBody = "";
        if (Request.ContentLength is > 0 && !Request.HasFormContentType)
        {
            // İmza ham gövde üzerinden doğrulanır — gövde değiştirilmeden okunur.
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
        }

        var form = new Dictionary<string, string>();
        if (Request.HasFormContentType)
        {
            foreach (var field in Request.Form)
                form[field.Key] = field.Value.ToString();
        }

        return new PaymentCallbackContext
        {
            RawBody = rawBody,
            Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Form = form,
            Query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString()),
        };
    }

    private async Task<CheckoutViewModel> BuildViewModelAsync(CheckoutFormModel form, Plan plan)
    {
        // Özet, kullanıcının seçtiği ülkenin KDV oranıyla yeniden hesaplanır.
        var quote = await _checkoutService.BuildQuoteAsync(
                        form.PlanId, form.BillingPeriod, form.CouponCode, form.Country)
                    ?? new CheckoutQuote();

        var currentPlan = await _planService.GetUserActivePlanAsync(_userManager.GetUserId(User)!);

        // Jeton yenilenmez: aynı form tekrar gönderildiğinde idempotency
        // anahtarı değişmemeli, aksi hâlde çift sipariş oluşabilir.
        return new CheckoutViewModel
        {
            Form = form,
            Quote = quote,
            Plan = plan,
            CurrentPlanName = currentPlan.Name,
        };
    }

    private static string PeriodLabel(string billingPeriod) =>
        billingPeriod == BillingPeriods.Yearly ? "Yıllık" : "Aylık";

    /// <summary>
    /// Kayıtlı telefondan ülke kodunu ve baştaki sıfırı ayıklar — form alanı
    /// yalnızca ulusal numarayı gösterir, ülke kodu ayrı ön ekte durur.
    /// </summary>
    private static string NationalPhonePart(string? phone, string countryCode)
    {
        var digits = BillingContactRules.DigitsOnly(phone);
        if (digits.Length == 0)
            return "";

        var dial = Countries.DialCodeFor(countryCode);
        if (digits.StartsWith(dial, StringComparison.Ordinal))
            digits = digits[dial.Length..];

        return digits.TrimStart('0');
    }
}

using Eascess_Application.Services;
using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Eascess.Tests.Integration;

/// <summary>
/// 20 farklı kullanıcı / plan varyasyonu üzerinden fiyatlandırma vaatlerinin
/// kodda gerçekten uygulandığını doğrulayan uçtan uca test matrisi.
/// Sonuçlar docs/plan-uygulama-test-raporu.md dosyasında versiyonlu raporlanır.
/// </summary>
public class PlanEnforcementIntegrationTests : IClassFixture<PlanEnforcementTestFactory>
{
    private readonly PlanEnforcementTestFactory _factory;
    private readonly HttpClient _client;

    public PlanEnforcementIntegrationTests(PlanEnforcementTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.EnsureSeeded();
    }

    // ── Kullanıcı matrisi: 20 kullanıcı, beklenen çözümlenen plan ────────────
    // Varyasyonlar: aboneliksiz, süresi dolmuş, pasif, silinmiş, gelecekte
    // başlayan, deneme, çoklu abonelik, kademe çakışması, downgrade...
    public static TheoryData<string, int> KullaniciPlanMatrisi => new()
    {
        { "u01-free-nosub",           PlanIds.Free },       // hiç aboneliği yok
        { "u02-free-expired-pro",     PlanIds.Free },       // Pro 10 gün önce bitti
        { "u03-free-inactive-pro",    PlanIds.Free },       // Pro tarihçe içinde ama IsActive=false
        { "u04-free-deleted-ultra",   PlanIds.Free },       // Ultra aboneliği soft-delete edilmiş
        { "u05-free-future-pro",      PlanIds.Free },       // Pro 10 gün SONRA başlayacak
        { "u06-pro-active",           PlanIds.Pro },        // standart aktif Pro
        { "u07-pro-trial",            PlanIds.Pro },        // 14 günlük deneme (kayıt akışıyla aynı kurgu)
        { "u08-pro-plus-free",        PlanIds.Pro },        // Pro + Ücretsiz aynı anda aktif
        { "u09-pro-expiring-soon",    PlanIds.Pro },        // Pro 1 saat sonra bitiyor (hâlâ geçerli)
        { "u10-pro-domain-limit",     PlanIds.Pro },        // Pro, 3 domain'le limitte
        { "u11-ultra-active",         PlanIds.Ultra },      // standart aktif Ultra
        { "u12-ultra-plus-pro",       PlanIds.Ultra },      // Ultra + Pro aktif → üst kademe kazanır
        { "u13-ultra-fresh",          PlanIds.Ultra },      // Ultra bugün başladı
        { "u14-ultra-yearly",         PlanIds.Ultra },      // yıllık Ultra (bitiş +1 yıl)
        { "u15-ultra-downgrade-plan", PlanIds.Ultra },      // Ultra aktif + gelecekte Ücretsiz satırı
        { "u16-ent-active",           PlanIds.Enterprise }, // standart Kurumsal
        { "u17-ent-plus-pro",         PlanIds.Enterprise }, // Kurumsal(fiyat=0) + Pro(600₺) → kademe kazanmalı
        { "u18-ent-longterm",         PlanIds.Enterprise }, // 10 yıllık Kurumsal
        { "u19-free-trial-ended",     PlanIds.Free },       // deneme dün bitti → Ücretsiz'e düştü
        { "u20-free-downgraded",      PlanIds.Free },       // Pro'dan düşmüş, widget'ı özelleştirilmişti
    };

    [Theory]
    [MemberData(nameof(KullaniciPlanMatrisi))]
    public async Task PlanCozunurlugu_BeklenenPlanaCozumlenir(string userId, int beklenenPlanId)
    {
        using var scope = _factory.Services.CreateScope();
        var planService = scope.ServiceProvider.GetRequiredService<IPlanService>();

        var plan = await planService.GetUserActivePlanAsync(userId);

        Assert.Equal(beklenenPlanId, plan.Id);
    }

    // ── Özellik kapıları: çözümlenen plan, vaat matrisiyle uyumlu mu ─────────

    [Theory]
    [InlineData("u01-free-nosub",       false, false, false, false)]
    [InlineData("u19-free-trial-ended", false, false, false, false)]
    [InlineData("u06-pro-active",       true,  true,  true,  false)]
    [InlineData("u07-pro-trial",        true,  true,  true,  false)]
    [InlineData("u11-ultra-active",     true,  true,  true,  true)]
    [InlineData("u16-ent-active",       true,  true,  true,  true)]
    [InlineData("u17-ent-plus-pro",     true,  true,  true,  true)]
    public async Task OzellikKapilari_VaatMatrisiyleUyumlu(
        string userId, bool widget, bool rapor, bool otoTarama, bool eposta)
    {
        using var scope = _factory.Services.CreateScope();
        var planService = scope.ServiceProvider.GetRequiredService<IPlanService>();

        var plan = await planService.GetUserActivePlanAsync(userId);

        Assert.Equal(widget,    plan.HasWidgetCustomization);
        Assert.Equal(rapor,     plan.HasDetailedReports);
        Assert.Equal(otoTarama, plan.HasAutoRescan);
        Assert.Equal(eposta,    plan.HasEmailNotifications);
    }

    // ── AI kotası: ücretsiz planlar API'de reddedilir, ücretliler geçer ──────

    [Theory]
    [InlineData("u01-free-nosub",       HttpStatusCode.TooManyRequests)] // kota 0
    [InlineData("u19-free-trial-ended", HttpStatusCode.TooManyRequests)] // deneme bitti → kota 0
    [InlineData("u06-pro-active",       HttpStatusCode.OK)]
    [InlineData("u11-ultra-active",     HttpStatusCode.OK)]
    [InlineData("u16-ent-active",       HttpStatusCode.OK)]
    public async Task AiTarama_PlanKotasinaGoreYanitlar(string userId, HttpStatusCode beklenen)
    {
        var licenseKey = _factory.LicenseKeyOf(userId);

        var response = await _client.PostAsJsonAsync("/api/scan/alt-text", new
        {
            licenseKey,
            images = new[] { $"https://example.com/{userId}.jpg" }
        });

        Assert.Equal(beklenen, response.StatusCode);

        if (beklenen == HttpStatusCode.TooManyRequests)
        {
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("QUOTA_EXCEEDED", json.GetProperty("code").GetString());
        }
    }

    // ── Domain limiti kuralı: DomainsController.Add ile aynı kural ───────────

    [Theory]
    [InlineData("u01-free-nosub",   1)] // Ücretsiz: 1 domain → limitte, yeni eklenemez
    [InlineData("u10-pro-domain-limit", 3)] // Pro: 3 domain → limitte
    public async Task DomainLimiti_LimittekiKullaniciYeniDomainEkleyemez(string userId, int beklenenLimit)
    {
        using var scope = _factory.Services.CreateScope();
        var planService = scope.ServiceProvider.GetRequiredService<IPlanService>();
        var domainRepo  = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();

        var plan = await planService.GetUserActivePlanAsync(userId);
        var aktifDomainSayisi = (await domainRepo.FindAsync(
            d => d.UserId == userId && d.IsDeleted != true)).Count();

        Assert.Equal(beklenenLimit, plan.MaxDomains);
        // DomainsController.Add'daki kuralın kendisi: sayı >= limit → engelle
        Assert.True(aktifDomainSayisi >= plan.MaxDomains,
            $"{userId} limitte olmalıydı: {aktifDomainSayisi}/{plan.MaxDomains}");
    }

    [Fact]
    public async Task DomainLimiti_UltraKullanicininYeri_Var()
    {
        using var scope = _factory.Services.CreateScope();
        var planService = scope.ServiceProvider.GetRequiredService<IPlanService>();
        var domainRepo  = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();

        var plan = await planService.GetUserActivePlanAsync("u11-ultra-active");
        var aktif = (await domainRepo.FindAsync(
            d => d.UserId == "u11-ultra-active" && d.IsDeleted != true)).Count();

        Assert.Equal(10, plan.MaxDomains);
        Assert.True(aktif < plan.MaxDomains); // 1/10 → ekleme serbest
    }

    // ── E-posta bildirimleri: aylık rapor yalnızca Ultra/Kurumsal'a gider ────

    [Fact]
    public async Task AylikRaporEpostasi_YalnizcaUltraVeKurumsalaGider()
    {
        using var scope = _factory.Services.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IMonthlyReportService>();
        var email = (RecordingEmailService)scope.ServiceProvider.GetRequiredService<IEmailService>();
        email.Recipients.Clear();

        await reportService.GenerateForAllDomainsAsync(2026, 6);

        // Doğrulanmış domain'i olan kullanıcılar: u06 (Pro), u11 (Ultra), u16 (Kurumsal), u20 (Ücretsiz)
        Assert.Contains("u11-ultra-active@test.local", email.Recipients);
        Assert.Contains("u16-ent-active@test.local", email.Recipients);
        Assert.DoesNotContain("u06-pro-active@test.local", email.Recipients);   // Pro'da e-posta vaadi yok
        Assert.DoesNotContain("u20-free-downgraded@test.local", email.Recipients); // Ücretsiz'de hiç yok
    }

    // ── Widget config: ücretsiz plana düşen kullanıcının özelleştirmesi ──────
    // Vaat: widget özelleştirme Pro ve üzeri. Pro'dan düşen kullanıcının widget'ı
    // varsayılan görünüme dönmeli; aksi halde özelleştirme ücretsiz planda da
    // fiilen çalışmaya devam eder.

    [Fact]
    public async Task WidgetConfig_UcretsizeDusenKullanici_VarsayilanaDoner()
    {
        var licenseKey = _factory.LicenseKeyOf("u20-free-downgraded");

        var response = await _client.GetAsync($"/api/widget/config?key={licenseKey}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        // Seed'de tema "#ff0000", konum "top-left", logo dolu — plan artık Ücretsiz.
        Assert.Equal("#0056b3",      json.GetProperty("themeColor").GetString());
        Assert.Equal("bottom-right", json.GetProperty("position").GetString());
        Assert.True(json.GetProperty("logoUrl").ValueKind is JsonValueKind.Null);
    }

    [Fact]
    public async Task WidgetConfig_AktifProKullanici_OzellestirmesiKorunur()
    {
        var licenseKey = _factory.LicenseKeyOf("u06-pro-active");

        var response = await _client.GetAsync($"/api/widget/config?key={licenseKey}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("#22aa55", json.GetProperty("themeColor").GetString());
        Assert.Equal("top-right", json.GetProperty("position").GetString());
    }
}

/// <summary>
/// 20 kullanıcılı plan matrisi factory'si: Gemini yerine fake generator,
/// SMTP yerine kayıt tutan fake e-posta servisi.
/// </summary>
public class PlanEnforcementTestFactory : EaccessWebAppFactory
{
    private readonly Dictionary<string, Guid> _licenseKeys = new();
    private readonly object _seedLock = new();
    private bool _seeded;

    public Guid LicenseKeyOf(string userId) => _licenseKeys[userId];

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAltTextGeneratorService>();
            services.AddScoped<IAltTextGeneratorService, FakeAltTextGeneratorService>();

            // E-posta gönderimini kaydeden fake — kimlere gittiği assert edilir
            services.RemoveAll<IEmailService>();
            services.AddSingleton<RecordingEmailService>();
            services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<RecordingEmailService>());
        });
    }

    public void EnsureSeeded()
    {
        lock (_seedLock)
        {
            if (_seeded) return;
            SeedDatabase(Seed);
            _seeded = true;
        }
    }

    private void Seed(Eascess_Infrastructure.Persistence.EaccessDbContext db)
    {
        var now = DateTime.UtcNow;

        void User(string id) => db.Users.Add(new AppUser
        {
            Id = id,
            UserName = $"{id}@test.local",
            Email = $"{id}@test.local",
            FullName = id,
        });

        void Sub(string userId, int planId, DateTime start, DateTime end,
                 bool isActive = true, bool isDeleted = false)
            => db.UserSubscriptions.Add(new UserSubscription
            {
                UserId = userId, PlanId = planId,
                StartDate = start, EndDate = end,
                IsActive = isActive, IsDeleted = isDeleted,
            });

        Domain Dom(string userId, string url, bool verified = true)
        {
            var d = new Domain
            {
                UserId = userId, DomainUrl = url,
                LicenseKey = Guid.NewGuid(),
                IsVerified = verified, IsDeleted = false,
                CreatedAt = now,
            };
            db.Domains.Add(d);
            _licenseKeys[userId] = d.LicenseKey;
            return d;
        }

        // ── Ücretsiz varyasyonları ────────────────────────────────────────
        User("u01-free-nosub");
        User("u02-free-expired-pro");
        Sub("u02-free-expired-pro", 2, now.AddDays(-40), now.AddDays(-10));
        User("u03-free-inactive-pro");
        Sub("u03-free-inactive-pro", 2, now.AddDays(-5), now.AddDays(25), isActive: false);
        User("u04-free-deleted-ultra");
        Sub("u04-free-deleted-ultra", 4, now.AddDays(-5), now.AddDays(25), isDeleted: true);
        User("u05-free-future-pro");
        Sub("u05-free-future-pro", 2, now.AddDays(10), now.AddDays(40));

        // ── Pro varyasyonları ─────────────────────────────────────────────
        User("u06-pro-active");
        Sub("u06-pro-active", 2, now.AddDays(-5), now.AddDays(25));
        User("u07-pro-trial"); // kayıt akışının birebir kopyası
        Sub("u07-pro-trial", 2, now, now.AddDays(14));
        Sub("u07-pro-trial", 1, now.AddDays(14), now.AddYears(100));
        User("u08-pro-plus-free");
        Sub("u08-pro-plus-free", 2, now.AddDays(-5), now.AddDays(25));
        Sub("u08-pro-plus-free", 1, now.AddDays(-5), now.AddYears(100));
        User("u09-pro-expiring-soon");
        Sub("u09-pro-expiring-soon", 2, now.AddDays(-29), now.AddHours(1));
        User("u10-pro-domain-limit");
        Sub("u10-pro-domain-limit", 2, now.AddDays(-5), now.AddDays(25));

        // ── Ultra varyasyonları ───────────────────────────────────────────
        User("u11-ultra-active");
        Sub("u11-ultra-active", 4, now.AddDays(-5), now.AddDays(25));
        User("u12-ultra-plus-pro");
        Sub("u12-ultra-plus-pro", 4, now.AddDays(-5), now.AddDays(25));
        Sub("u12-ultra-plus-pro", 2, now.AddDays(-5), now.AddDays(25));
        User("u13-ultra-fresh");
        Sub("u13-ultra-fresh", 4, now.AddMinutes(-1), now.AddDays(30));
        User("u14-ultra-yearly");
        Sub("u14-ultra-yearly", 4, now.AddDays(-5), now.AddYears(1));
        User("u15-ultra-downgrade-plan");
        Sub("u15-ultra-downgrade-plan", 4, now.AddDays(-5), now.AddDays(25));
        Sub("u15-ultra-downgrade-plan", 1, now.AddDays(25), now.AddYears(100));

        // ── Kurumsal varyasyonları ────────────────────────────────────────
        User("u16-ent-active");
        Sub("u16-ent-active", 3, now.AddDays(-5), now.AddYears(1));
        User("u17-ent-plus-pro"); // Kurumsal fiyat=0 — kademe sıralaması testi
        Sub("u17-ent-plus-pro", 3, now.AddDays(-5), now.AddYears(1));
        Sub("u17-ent-plus-pro", 2, now.AddDays(-5), now.AddDays(25));
        User("u18-ent-longterm");
        Sub("u18-ent-longterm", 3, now.AddDays(-5), now.AddYears(10));

        // ── Downgrade varyasyonları ───────────────────────────────────────
        User("u19-free-trial-ended"); // deneme dün bitti
        Sub("u19-free-trial-ended", 2, now.AddDays(-15), now.AddDays(-1));
        Sub("u19-free-trial-ended", 1, now.AddDays(-1), now.AddYears(100));
        User("u20-free-downgraded"); // Pro'dan düşmüş, widget'ı özelleştirilmiş
        Sub("u20-free-downgraded", 2, now.AddDays(-60), now.AddDays(-3));

        // ── Domain + widget ayarları ──────────────────────────────────────
        var d01 = Dom("u01-free-nosub", "u01-free.test");
        var d06 = Dom("u06-pro-active", "u06-pro.test");
        var d11 = Dom("u11-ultra-active", "u11-ultra.test");
        var d16 = Dom("u16-ent-active", "u16-ent.test");
        var d19 = Dom("u19-free-trial-ended", "u19-trial-ended.test");
        var d20 = Dom("u20-free-downgraded", "u20-downgraded.test");
        // u10: Pro limitinde — 3 domain (lisans anahtarı sözlüğünde son eklenen kalır, sorun değil)
        Dom("u10-pro-domain-limit", "u10-a.test");
        Dom("u10-pro-domain-limit", "u10-b.test");
        Dom("u10-pro-domain-limit", "u10-c.test");

        db.SaveChanges(); // domain Id'leri oluşsun

        void Widget(Domain d, string theme = "#0056b3", string pos = "bottom-right",
                    string? logo = null, bool ai = true)
            => db.WidgetSettings.Add(new WidgetSetting
            {
                DomainId = d.Id, IsActive = true, IsAiEnabled = ai,
                ThemeColor = theme, Position = pos, Language = "tr", LogoUrl = logo,
            });

        Widget(d01);
        Widget(d06, theme: "#22aa55", pos: "top-right"); // Pro — özelleştirme meşru
        Widget(d11);
        Widget(d16);
        Widget(d19);
        // u20: Pro döneminde yapılmış özelleştirme — plan artık Ücretsiz
        Widget(d20, theme: "#ff0000", pos: "top-left", logo: "/uploads/logos/u20/logo.png");
    }
}

/// <summary>SMTP yerine geçen, alıcıları kaydeden fake e-posta servisi.</summary>
public class RecordingEmailService : IEmailService
{
    public List<string> Recipients { get; } = new();

    public Task SendAsync(string toAddress, string toName, string subject, string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null, CancellationToken ct = default)
    {
        lock (Recipients) Recipients.Add(toAddress);
        return Task.CompletedTask;
    }
}

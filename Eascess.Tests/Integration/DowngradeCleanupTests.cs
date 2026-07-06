using Eascess_Application.Services;
using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Eascess.Tests.Integration;

/// <summary>
/// Plan düşüşü temizliği (v3 ürün kararları) senaryoları:
/// - Ücretsiz'e düşen kullanıcının TÜM domainleri silinir, yeniden bağlaması gerekir.
/// - Logolar son ücretli abonelik bitişinden itibaren 60 gün bekletilir;
///   kullanıcı bu sürede dönerse kalır, dönmezse silinir.
/// Sonuçlar docs/plan-uygulama-test-raporu.md dosyasında raporlanır.
/// </summary>
public class DowngradeCleanupTests : IClassFixture<EaccessWebAppFactory>
{
    private readonly EaccessWebAppFactory _factory;
    private static readonly DateTime Now = DateTime.UtcNow;

    public DowngradeCleanupTests(EaccessWebAppFactory factory)
    {
        _factory = factory;
        SeedOnce();
    }

    private static bool _seeded;
    private static readonly object _lock = new();

    private void SeedOnce()
    {
        lock (_lock)
        {
            if (_seeded) return;

            _factory.SeedDatabase(db =>
            {
                void Sub(string userId, int planId, DateTime start, DateTime end, bool isActive = true)
                    => db.UserSubscriptions.Add(new UserSubscription
                    {
                        UserId = userId, PlanId = planId,
                        StartDate = start, EndDate = end,
                        IsActive = isActive, IsDeleted = false,
                    });

                Domain Dom(string userId, string url)
                {
                    var d = new Domain
                    {
                        UserId = userId, DomainUrl = url, LicenseKey = Guid.NewGuid(),
                        IsVerified = true, IsDeleted = false, CreatedAt = Now,
                    };
                    db.Domains.Add(d);
                    return d;
                }

                // v01: Pro dün bitti, 2 domain → domainleri silinmeli
                Sub("v01-pro-expired", PlanIds.Pro, Now.AddDays(-30), Now.AddDays(-1));
                Sub("v01-pro-expired", PlanIds.Free, Now.AddDays(-1), Now.AddYears(100), isActive: true);
                var v01a = Dom("v01-pro-expired", "v01-a.test");
                var v01b = Dom("v01-pro-expired", "v01-b.test");

                // v02: Ultra aktif + Pro dün bitti → domainler KALMALI
                Sub("v02-ultra-keeps", PlanIds.Ultra, Now.AddDays(-30), Now.AddDays(30));
                Sub("v02-ultra-keeps", PlanIds.Pro, Now.AddDays(-30), Now.AddDays(-1));
                var v02a = Dom("v02-ultra-keeps", "v02-a.test");

                // v03: Ücretli abonelik 61 gün önce bitmiş (zaten pasif), logo dolu → logo SİLİNMELİ
                Sub("v03-logo-purge", PlanIds.Pro, Now.AddDays(-120), Now.AddDays(-61), isActive: false);
                var v03a = Dom("v03-logo-purge", "v03-a.test");

                // v04: Ücretli abonelik 10 gün önce bitmiş, logo dolu → bekleme sürüyor, logo KALMALI
                Sub("v04-logo-waiting", PlanIds.Pro, Now.AddDays(-40), Now.AddDays(-10), isActive: false);
                var v04a = Dom("v04-logo-waiting", "v04-a.test");

                // v05: Eski abonelik 61 gün önce bitmiş AMA kullanıcı Pro'ya geri dönmüş → logo KALMALI
                Sub("v05-returned", PlanIds.Pro, Now.AddDays(-120), Now.AddDays(-61), isActive: false);
                Sub("v05-returned", PlanIds.Pro, Now.AddDays(-5), Now.AddDays(25));
                var v05a = Dom("v05-returned", "v05-a.test");

                db.SaveChanges(); // domain Id'leri

                void Widget(Domain d, string? logo = null)
                    => db.WidgetSettings.Add(new WidgetSetting
                    {
                        DomainId = d.Id, IsActive = true, IsAiEnabled = true,
                        ThemeColor = "#123456", Position = "bottom-right", Language = "tr",
                        LogoUrl = logo,
                    });

                Widget(v01a); Widget(v01b);
                Widget(v02a, logo: "/uploads/logos/v02/logo.png");
                Widget(v03a, logo: "/uploads/logos/v03/logo.png");
                Widget(v04a, logo: "/uploads/logos/v04/logo.png");
                Widget(v05a, logo: "/uploads/logos/v05/logo.png");
            });

            // Temizliği bir kez, tüm senaryolar için çalıştır
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDowngradeCleanupService>();
            service.RunAsync(Now).GetAwaiter().GetResult();

            _seeded = true;
        }
    }

    private T InScope<T>(Func<IServiceProvider, T> f)
    {
        using var scope = _factory.Services.CreateScope();
        return f(scope.ServiceProvider);
    }

    [Fact]
    public async Task ProSuresiDolunca_TumDomainleriSilinir_YenidenBaglamasiGerekir()
    {
        using var scope = _factory.Services.CreateScope();
        var domainRepo = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();
        var subRepo = scope.ServiceProvider.GetRequiredService<IRepository<UserSubscription>>();

        var aktifDomainler = await domainRepo.FindAsync(
            d => d.UserId == "v01-pro-expired" && d.IsDeleted != true);
        Assert.Empty(aktifDomainler); // her ikisi de silindi

        var silinmis = await domainRepo.FindAsync(
            d => d.UserId == "v01-pro-expired" && d.IsDeleted == true);
        Assert.Equal(2, silinmis.Count());
        Assert.All(silinmis, d => Assert.NotNull(d.DeletedAt));

        // Süresi dolan Pro pasifleştirildi, Ücretsiz aktive edildi
        var proSub = await subRepo.FirstOrDefaultAsync(
            s => s.UserId == "v01-pro-expired" && s.PlanId == PlanIds.Pro);
        Assert.False(proSub!.IsActive);
    }

    [Fact]
    public async Task ProSuresiDolunca_WidgetAyarlariDaPasiflesir()
    {
        using var scope = _factory.Services.CreateScope();
        var domainRepo = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();
        var wsRepo = scope.ServiceProvider.GetRequiredService<IRepository<WidgetSetting>>();

        var domainler = await domainRepo.FindAsync(d => d.UserId == "v01-pro-expired");
        foreach (var d in domainler)
        {
            var ayarlar = await wsRepo.FindAsync(w => w.DomainId == d.Id);
            Assert.All(ayarlar, w => Assert.False(w.IsActive));
        }
    }

    [Fact]
    public async Task BaskaUcretliPlaniVarsa_DomainlerKalir()
    {
        using var scope = _factory.Services.CreateScope();
        var domainRepo = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();
        var planService = scope.ServiceProvider.GetRequiredService<IPlanService>();

        // Ultra hâlâ aktif → plan Ultra, domainlere dokunulmadı
        var plan = await planService.GetUserActivePlanAsync("v02-ultra-keeps");
        Assert.Equal(PlanIds.Ultra, plan.Id);

        var aktif = await domainRepo.FindAsync(
            d => d.UserId == "v02-ultra-keeps" && d.IsDeleted != true);
        Assert.Single(aktif);
    }

    [Fact]
    public async Task Logo_60GunDolunca_KaliciSilinir()
    {
        using var scope = _factory.Services.CreateScope();
        var wsRepo = scope.ServiceProvider.GetRequiredService<IRepository<WidgetSetting>>();
        var domainRepo = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();

        var domain = await domainRepo.FirstOrDefaultAsync(d => d.UserId == "v03-logo-purge");
        var ayar = await wsRepo.FirstOrDefaultAsync(w => w.DomainId == domain!.Id);

        Assert.Null(ayar!.LogoUrl); // 61 gün geçti → temizlendi
    }

    [Fact]
    public async Task Logo_60GunDolmadan_Bekletilir()
    {
        using var scope = _factory.Services.CreateScope();
        var wsRepo = scope.ServiceProvider.GetRequiredService<IRepository<WidgetSetting>>();
        var domainRepo = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();

        var domain = await domainRepo.FirstOrDefaultAsync(d => d.UserId == "v04-logo-waiting");
        var ayar = await wsRepo.FirstOrDefaultAsync(w => w.DomainId == domain!.Id);

        Assert.Equal("/uploads/logos/v04/logo.png", ayar!.LogoUrl); // 10 gün — bekleme sürüyor
    }

    [Fact]
    public async Task Logo_KullaniciGeriDonduyse_Korunur()
    {
        using var scope = _factory.Services.CreateScope();
        var wsRepo = scope.ServiceProvider.GetRequiredService<IRepository<WidgetSetting>>();
        var domainRepo = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();

        var domain = await domainRepo.FirstOrDefaultAsync(d => d.UserId == "v05-returned");
        var ayar = await wsRepo.FirstOrDefaultAsync(w => w.DomainId == domain!.Id);

        // Eski abonelik 61 gün önce bitmişti ama kullanıcı Pro'ya geri döndü → logo kalır
        Assert.Equal("/uploads/logos/v05/logo.png", ayar!.LogoUrl);
    }

    [Fact]
    public async Task UcretliPlaniAktifKullanicininLogosu_HicIslenmez()
    {
        using var scope = _factory.Services.CreateScope();
        var wsRepo = scope.ServiceProvider.GetRequiredService<IRepository<WidgetSetting>>();
        var domainRepo = scope.ServiceProvider.GetRequiredService<IRepository<Domain>>();

        var domain = await domainRepo.FirstOrDefaultAsync(d => d.UserId == "v02-ultra-keeps");
        var ayar = await wsRepo.FirstOrDefaultAsync(w => w.DomainId == domain!.Id);

        Assert.Equal("/uploads/logos/v02/logo.png", ayar!.LogoUrl);
    }
}

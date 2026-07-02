using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Moq;
using System.Linq.Expressions;

namespace Eascess.Tests.Unit;

public class AiScanServiceTests
{
    private readonly Mock<IRepository<Domain>> _domainRepo = new();
    private readonly Mock<IRepository<WidgetSetting>> _widgetSettingRepo = new();
    private readonly Mock<IRepository<ImageAltTextCache>> _cacheRepo = new();
    private readonly Mock<IRepository<AiUsageLog>> _usageLogRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAltTextGeneratorService> _generator = new();
    private readonly Mock<IPlanService> _planService = new();
    private readonly AiScanService _sut;

    private static readonly Guid TestKey = Guid.NewGuid();
    private const int TestMonthlyQuota = 100;

    public AiScanServiceTests()
    {
        // Varsayılan plan: MonthlyAiQuota = 100
        _planService.Setup(p => p.GetUserActivePlanAsync(It.IsAny<string>()))
            .ReturnsAsync(new Eascess_Domain.Entities.Plan { Id = 1, Name = "Test", MaxDomains = 10, MonthlyAiQuota = TestMonthlyQuota });

        _sut = new AiScanService(
            _domainRepo.Object,
            _widgetSettingRepo.Object,
            _cacheRepo.Object,
            _usageLogRepo.Object,
            _unitOfWork.Object,
            _generator.Object,
            _planService.Object);
    }

    [Fact]
    public async Task ProcessImages_GeçerliKeyVeGörseller_SonuçDöner()
    {
        SetupValidDomain();
        SetupEmptyQuota();
        SetupNoCache();
        _generator.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AltTextResult(true, "Bir kedi resmi", null, 200));

        var result = await _sut.ProcessImagesAsync(TestKey, new List<string> { "https://example.com/cat.jpg" });

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.Equal("Bir kedi resmi", result.Results[0].AltText);
        Assert.False(result.Results[0].FromCache);
        Assert.Equal(1, result.ProcessedCount);
    }

    [Fact]
    public async Task ProcessImages_GeçersizLisans_ThrowsInvalidLicense()
    {
        _domainRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync((Domain?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ProcessImagesAsync(Guid.NewGuid(), new List<string> { "https://example.com/img.jpg" }));

        Assert.Equal("INVALID_LICENSE", ex.Message);
    }

    [Fact]
    public async Task ProcessImages_AiDevreDışı_ThrowsAiDisabled()
    {
        SetupValidDomain();
        _widgetSettingRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<WidgetSetting, bool>>>()))
            .ReturnsAsync(new WidgetSetting { IsAiEnabled = false, IsActive = true });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ProcessImagesAsync(TestKey, new List<string> { "https://example.com/img.jpg" }));

        Assert.Equal("AI_DISABLED", ex.Message);
    }

    [Fact]
    public async Task ProcessImages_KotaAşıldı_ThrowsQuotaExceeded()
    {
        SetupValidDomain();
        // 100 kullanım kaydı oluştur (kota: 100)
        var logs = Enumerable.Range(0, 100)
            .Select(i => new AiUsageLog { DomainId = 1, RequestDate = DateTime.UtcNow })
            .ToList();
        _usageLogRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<AiUsageLog, bool>>>()))
            .ReturnsAsync(logs);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ProcessImagesAsync(TestKey, new List<string> { "https://example.com/img.jpg" }));

        Assert.Equal("QUOTA_EXCEEDED", ex.Message);
    }

    [Fact]
    public async Task ProcessImages_KotaKullanıcıBazlıToplanır_DiğerDomainlerinKullanımıDaSayılır()
    {
        SetupValidDomain(); // istek domain Id=1, UserId=u1

        // Kullanıcının ikinci bir domaini daha var
        _domainRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync(new List<Domain>
            {
                new() { Id = 1, UserId = "u1", DomainUrl = "a.local" },
                new() { Id = 2, UserId = "u1", DomainUrl = "b.local" },
            });

        // Kota 100: domain 1'de 60 + domain 2'de 40 kullanım → toplam 100, kota dolu.
        // Domain bazlı sayılsaydı (eski hatalı davranış) 60 < 100 olur ve istek geçerdi.
        var logs = Enumerable.Range(0, 60).Select(_ => new AiUsageLog { DomainId = 1, RequestDate = DateTime.UtcNow })
            .Concat(Enumerable.Range(0, 40).Select(_ => new AiUsageLog { DomainId = 2, RequestDate = DateTime.UtcNow }))
            .ToList();
        _usageLogRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<AiUsageLog, bool>>>()))
            .ReturnsAsync((Expression<Func<AiUsageLog, bool>> pred) => logs.Where(pred.Compile()).ToList());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ProcessImagesAsync(TestKey, new List<string> { "https://example.com/img.jpg" }));

        Assert.Equal("QUOTA_EXCEEDED", ex.Message);
    }

    [Fact]
    public async Task ProcessImages_CacheHit_GeneratorÇağrılmaz()
    {
        SetupValidDomain();
        SetupEmptyQuota();
        _cacheRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<ImageAltTextCache, bool>>>()))
            .ReturnsAsync(new ImageAltTextCache
            {
                AltText = "Cached alt text",
                ExpiresAt = DateTime.UtcNow.AddDays(10)
            });

        var result = await _sut.ProcessImagesAsync(TestKey, new List<string> { "https://example.com/cached.jpg" });

        Assert.Single(result.Results);
        Assert.True(result.Results[0].FromCache);
        Assert.Equal("Cached alt text", result.Results[0].AltText);
        Assert.Equal(1, result.CachedCount);
        Assert.Equal(0, result.ProcessedCount);
        _generator.Verify(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessImages_GeneratorBaşarısız_DiğerGörsellerDevamEder()
    {
        SetupValidDomain();
        SetupEmptyQuota();
        SetupNoCache();

        var callCount = 0;
        _generator.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1) return new AltTextResult(false, null, "download_failed", 100);
                return new AltTextResult(true, "İkinci görsel", null, 200);
            });

        var result = await _sut.ProcessImagesAsync(TestKey,
            new List<string> { "https://example.com/broken.jpg", "https://example.com/good.jpg" });

        Assert.Equal(2, result.Results.Count);
        Assert.False(result.Results[0].Success);
        Assert.True(result.Results[1].Success);
        Assert.Equal("İkinci görsel", result.Results[1].AltText);
    }

    [Fact]
    public async Task ProcessImages_DuplicateUrlFiltrelenir()
    {
        SetupValidDomain();
        SetupEmptyQuota();
        SetupNoCache();
        _generator.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AltTextResult(true, "Alt text", null, 100));

        var result = await _sut.ProcessImagesAsync(TestKey,
            new List<string> { "https://example.com/img.jpg", "https://example.com/img.jpg", "https://example.com/img.jpg" });

        Assert.Single(result.Results); // duplicate'ler filtrelendi
    }

    [Fact]
    public async Task ProcessImages_BaşarılıSonuçCacheYazılır()
    {
        SetupValidDomain();
        SetupEmptyQuota();
        SetupNoCache();
        _generator.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AltTextResult(true, "Yeni alt text", null, 150));

        await _sut.ProcessImagesAsync(TestKey, new List<string> { "https://example.com/new.jpg" });

        _cacheRepo.Verify(r => r.AddAsync(It.Is<ImageAltTextCache>(c =>
            c.AltText == "Yeni alt text" && c.Provider == "Gemini")), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ProcessImages_HerİstekUsageLogYazılır()
    {
        SetupValidDomain();
        SetupEmptyQuota();
        SetupNoCache();
        _generator.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AltTextResult(true, "Alt", null, 100));

        await _sut.ProcessImagesAsync(TestKey,
            new List<string> { "https://example.com/a.jpg", "https://example.com/b.jpg" });

        _usageLogRepo.Verify(r => r.AddAsync(It.IsAny<AiUsageLog>()), Times.Exactly(2));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetupValidDomain()
    {
        _domainRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync(new Domain
            {
                Id = 1, UserId = "u1", LicenseKey = TestKey,
                DomainUrl = "test.local", IsDeleted = false, CreatedAt = DateTime.UtcNow
            });

        _widgetSettingRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<WidgetSetting, bool>>>()))
            .ReturnsAsync(new WidgetSetting { IsAiEnabled = true, IsActive = true });
    }

    private void SetupEmptyQuota()
    {
        _usageLogRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<AiUsageLog, bool>>>()))
            .ReturnsAsync(new List<AiUsageLog>());
    }

    private void SetupNoCache()
    {
        _cacheRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<ImageAltTextCache, bool>>>()))
            .ReturnsAsync((ImageAltTextCache?)null);
    }
}

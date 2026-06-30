using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Moq;
using System.Linq.Expressions;

namespace Eascess.Tests.Unit;

public class LicenseValidationServiceTests
{
    private readonly Mock<IRepository<Domain>> _domainRepoMock;
    private readonly Mock<IPlanService> _planServiceMock;
    private readonly LicenseValidationService _sut;

    public LicenseValidationServiceTests()
    {
        _domainRepoMock = new Mock<IRepository<Domain>>();
        _planServiceMock = new Mock<IPlanService>();
        _planServiceMock.Setup(p => p.GetUserActivePlanAsync(It.IsAny<string>()))
            .ReturnsAsync(new Eascess_Domain.Entities.Plan { Id = 1, Name = "Ücretsiz", MaxDomains = 1, MonthlyAiQuota = 50 });

        _sut = new LicenseValidationService(_domainRepoMock.Object, _planServiceMock.Object);
    }

    // ─── ValidateAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_GeçerliKeyVeDomain_ReturnsValid()
    {
        var key = Guid.NewGuid();
        var domain = BuildDomain(key, "example.com");
        SetupRepo(domain);

        var result = await _sut.ValidateAsync(key, "example.com");

        Assert.True(result.Valid);
        Assert.Null(result.Reason);
        Assert.Equal("ücretsiz", result.Plan);
    }

    [Fact]
    public async Task ValidateAsync_GeçerliKey_OriginFormatındaDomain_ReturnsValid()
    {
        // widget.js Origin header'dan "https://example.com" gönderir
        var key = Guid.NewGuid();
        var domain = BuildDomain(key, "example.com");
        SetupRepo(domain);

        var result = await _sut.ValidateAsync(key, "https://example.com");

        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateAsync_FarklıDomain_ReturnsFalse()
    {
        var key = Guid.NewGuid();
        var domain = BuildDomain(key, "example.com");
        SetupRepo(domain);

        var result = await _sut.ValidateAsync(key, "hacker.com");

        Assert.False(result.Valid);
        Assert.Equal("invalid", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_KeyBulunamadı_ReturnsFalse()
    {
        _domainRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync((Domain?)null);

        var result = await _sut.ValidateAsync(Guid.NewGuid(), "example.com");

        Assert.False(result.Valid);
        Assert.Equal("invalid", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_SilinmisDomain_ReturnsFalse()
    {
        // Soft-deleted domain için repo null dönmeli (IsDeleted!=true filtresi var)
        _domainRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync((Domain?)null);

        var result = await _sut.ValidateAsync(Guid.NewGuid(), "example.com");

        Assert.False(result.Valid);
        Assert.Equal("invalid", result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_BosDomain_ReturnsFalse()
    {
        var result = await _sut.ValidateAsync(Guid.NewGuid(), "");

        Assert.False(result.Valid);
        _domainRepoMock.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_EmptyGuid_ReturnsFalse()
    {
        var result = await _sut.ValidateAsync(Guid.Empty, "example.com");

        Assert.False(result.Valid);
        _domainRepoMock.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_DomainPlanLimitiniAsiyor_ReturnsPlanExpired()
    {
        // Kullanıcı üst planda iken 2 domain eklemiş, ardından MaxDomains=1 olan
        // Ücretsiz plana düşmüş. Limiti aşan (daha yeni eklenen) domain plan_expired alır.
        var key = Guid.NewGuid();
        var older = new Domain
        {
            Id = 1, UserId = "user-1", LicenseKey = Guid.NewGuid(), DomainUrl = "old.com",
            IsDeleted = false, IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        var matched = new Domain
        {
            Id = 2, UserId = "user-1", LicenseKey = key, DomainUrl = "example.com",
            IsDeleted = false, IsVerified = true, CreatedAt = DateTime.UtcNow
        };

        _domainRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync(matched);
        _domainRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync(new[] { older, matched });

        var result = await _sut.ValidateAsync(key, "example.com");

        Assert.False(result.Valid);
        Assert.Equal("plan_expired", result.Reason);
    }

    // ─── DomainIsRegisteredAsync ─────────────────────────────────────────────

    [Fact]
    public async Task DomainIsRegisteredAsync_KayıtlıDomain_ReturnsTrue()
    {
        var domain = BuildDomain(Guid.NewGuid(), "example.com");
        SetupRepo(domain);

        var result = await _sut.DomainIsRegisteredAsync("https://example.com");

        Assert.True(result);
    }

    [Fact]
    public async Task DomainIsRegisteredAsync_KayıtsızDomain_ReturnsFalse()
    {
        _domainRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync((Domain?)null);

        var result = await _sut.DomainIsRegisteredAsync("https://unknown.com");

        Assert.False(result);
    }

    [Fact]
    public async Task DomainIsRegisteredAsync_BosDomain_ReturnsFalse()
    {
        var result = await _sut.DomainIsRegisteredAsync("");

        Assert.False(result);
        _domainRepoMock.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()), Times.Never);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Domain BuildDomain(Guid licenseKey, string domainUrl) => new()
    {
        Id = 1,
        UserId = "user-1",
        LicenseKey = licenseKey,
        DomainUrl = domainUrl,
        IsDeleted = false,
        IsVerified = true,
        CreatedAt = DateTime.UtcNow
    };

    private void SetupRepo(Domain domain)
    {
        _domainRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync(domain);
        // Plan kotası sıralaması için kullanıcının domain listesi
        _domainRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
            .ReturnsAsync(new[] { domain });
    }
}

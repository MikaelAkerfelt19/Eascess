using Eascess_Application.DTOs;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Moq;
using System.Linq.Expressions;

namespace Eascess.Tests.Unit;

/// <summary>
/// WidgetSettingService.UpdateAsync doğrulama testleri.
/// </summary>
public class WidgetSettingServiceTests
{
    private readonly Mock<IRepository<WidgetSetting>> _settingRepo = new();
    private readonly Mock<IRepository<Domain>>        _domainRepo  = new();
    private readonly Mock<IUnitOfWork>                _uow         = new();
    private readonly WidgetSettingService _sut;

    private static readonly Domain TestDomain = new()
    {
        Id = 1, UserId = "u1", DomainUrl = "example.com",
        IsDeleted = false, LicenseKey = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
    };

    public WidgetSettingServiceTests()
    {
        _domainRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
                   .ReturnsAsync(TestDomain);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _sut = new WidgetSettingService(_settingRepo.Object, _domainRepo.Object, _uow.Object);
    }

    private WidgetSetting BuildActiveSetting() => new()
    {
        Id = 1, DomainId = 1, IsActive = true,
        ThemeColor = "#38bdf8", Position = "bottom-right", Language = "tr",
    };

    private void SetupSettingRepo(WidgetSetting setting)
    {
        _settingRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<WidgetSetting, bool>>>()))
                    .ReturnsAsync(setting);
    }

    [Fact]
    public async Task UpdateAsync_GeçersizRenk_VarsayılanRenkKullanılır()
    {
        var setting = BuildActiveSetting();
        SetupSettingRepo(setting);

        await _sut.UpdateAsync(new WidgetSettingDto
        {
            DomainId = 1, ThemeColor = "<script>alert(1)</script>",
            Position = "bottom-right", Language = "tr",
        }, "u1");

        Assert.Equal("#38bdf8", setting.ThemeColor);
    }

    [Fact]
    public async Task UpdateAsync_GeçersizPozisyon_VarsayılanPozisyonKullanılır()
    {
        var setting = BuildActiveSetting();
        SetupSettingRepo(setting);

        await _sut.UpdateAsync(new WidgetSettingDto
        {
            DomainId = 1, ThemeColor = "#ff0000",
            Position = "invalid-position", Language = "tr",
        }, "u1");

        Assert.Equal("bottom-right", setting.Position);
    }

    [Fact]
    public async Task UpdateAsync_GeçersizDil_VarsayılanDilKullanılır()
    {
        var setting = BuildActiveSetting();
        SetupSettingRepo(setting);

        await _sut.UpdateAsync(new WidgetSettingDto
        {
            DomainId = 1, ThemeColor = "#ff0000",
            Position = "top-left", Language = "xx",
        }, "u1");

        Assert.Equal("tr", setting.Language);
    }

    [Fact]
    public async Task UpdateAsync_GeçerliDeğerler_DeğiştirilmezKaydedilir()
    {
        var setting = BuildActiveSetting();
        SetupSettingRepo(setting);

        await _sut.UpdateAsync(new WidgetSettingDto
        {
            DomainId = 1, ThemeColor = "#818cf8",
            Position = "top-right", Language = "en",
        }, "u1");

        Assert.Equal("#818cf8", setting.ThemeColor);
        Assert.Equal("top-right", setting.Position);
        Assert.Equal("en", setting.Language);
    }

    [Fact]
    public async Task UpdateAsync_DomainBulunamazsa_FalseRet()
    {
        _domainRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain, bool>>>()))
                   .ReturnsAsync((Domain?)null);

        var result = await _sut.UpdateAsync(new WidgetSettingDto
        {
            DomainId = 99, ThemeColor = "#38bdf8", Position = "bottom-right", Language = "tr",
        }, "u1");

        Assert.False(result);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_GeçerliHexRenk_KaydedilirTrue()
    {
        var setting = BuildActiveSetting();
        SetupSettingRepo(setting);

        var result = await _sut.UpdateAsync(new WidgetSettingDto
        {
            DomainId = 1, ThemeColor = "#4ade80",
            Position = "bottom-left", Language = "tr",
        }, "u1");

        Assert.True(result);
        _settingRepo.Verify(r => r.Update(It.IsAny<WidgetSetting>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}

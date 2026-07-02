using System.Text.RegularExpressions;
using Eascess_Application.DTOs;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class WidgetSettingService : IWidgetSettingService
{
    private readonly IRepository<WidgetSetting> _settingRepo;
    private readonly IRepository<Domain> _domainRepo;
    private readonly IUnitOfWork _unitOfWork;

    public WidgetSettingService(
        IRepository<WidgetSetting> settingRepo,
        IRepository<Domain> domainRepo,
        IUnitOfWork unitOfWork)
    {
        _settingRepo = settingRepo;
        _domainRepo = domainRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<WidgetSettingDto?> GetByDomainAsync(int domainId, string userId)
    {
        var domain = await _domainRepo.FirstOrDefaultAsync(
            d => d.Id == domainId && d.UserId == userId && d.IsDeleted != true);
        if (domain is null) return null;

        var setting = await _settingRepo.FirstOrDefaultAsync(
            w => w.DomainId == domainId && w.IsActive);

        // Yoksa varsayılanlarla oluştur
        if (setting is null)
        {
            await CreateDefaultAsync(domainId);
            setting = await _settingRepo.FirstOrDefaultAsync(w => w.DomainId == domainId && w.IsActive);
        }

        if (setting is null) return null;

        return new WidgetSettingDto
        {
            Id = setting.Id,
            DomainId = setting.DomainId,
            DomainUrl = domain.DomainUrl,
            ThemeColor = setting.ThemeColor ?? "#38bdf8",
            Position = setting.Position ?? "bottom-right",
            Language = setting.Language ?? "tr",
            IsAiEnabled = setting.IsAiEnabled,
            LogoUrl = setting.LogoUrl,
            WidgetTitle = setting.WidgetTitle,
            PoweredByVisible = setting.PoweredByVisible,
        };
    }

    public async Task<bool> UpdateAsync(WidgetSettingDto dto, string userId)
    {
        var domain = await _domainRepo.FirstOrDefaultAsync(
            d => d.Id == dto.DomainId && d.UserId == userId && d.IsDeleted != true);
        if (domain is null) return false;

        var setting = await _settingRepo.FirstOrDefaultAsync(
            w => w.DomainId == dto.DomainId && w.IsActive);

        if (setting is null)
        {
            await CreateDefaultAsync(dto.DomainId);
            setting = await _settingRepo.FirstOrDefaultAsync(w => w.DomainId == dto.DomainId && w.IsActive);
            if (setting is null) return false;
        }

        setting.ThemeColor = IsValidHexColor(dto.ThemeColor) ? dto.ThemeColor : "#38bdf8";
        setting.Position   = IsValidPosition(dto.Position)   ? dto.Position   : "bottom-right";
        setting.Language   = IsValidLanguage(dto.Language)   ? dto.Language   : "tr";
        setting.IsAiEnabled = dto.IsAiEnabled;
        setting.LogoUrl = NormalizeLogoUrl(dto.LogoUrl);
        setting.WidgetTitle = NormalizeWidgetTitle(dto.WidgetTitle);
        setting.PoweredByVisible = dto.PoweredByVisible;
        setting.UpdatedAt = DateTime.UtcNow;

        _settingRepo.Update(setting);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private static readonly string[] ValidPositions = ["bottom-right", "bottom-left", "top-right", "top-left"];
    private static readonly string[] ValidLanguages  = ["tr", "en"];
    private static bool IsValidHexColor(string? c) =>
        c is not null && Regex.IsMatch(c, @"^#[0-9a-fA-F]{6}$");
    private static bool IsValidPosition(string? p) =>
        p is not null && ValidPositions.Contains(p);
    private static bool IsValidLanguage(string? l) =>
        l is not null && ValidLanguages.Contains(l);

    // LogoUrl ve WidgetTitle widget.js tarafından müşteri sitesinin DOM'una gömülür;
    // tırnak/açılı ayraç içeren değerler HTML attribute'undan kaçıp XSS'e yol açar.
    private static string? NormalizeLogoUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.Length > 500) return null;

        var isHttpOrRooted = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                          || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                          || trimmed.StartsWith('/');

        return isHttpOrRooted && !Regex.IsMatch(trimmed, @"[""'<>\s\\]") ? trimmed : null;
    }

    private static string? NormalizeWidgetTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var cleaned = Regex.Replace(title.Trim(), @"[<>""']", "");
        if (cleaned.Length > 30) cleaned = cleaned[..30];
        return cleaned.Length == 0 ? null : cleaned;
    }

    public async Task CreateDefaultAsync(int domainId)
    {
        var setting = new WidgetSetting
        {
            DomainId = domainId,
            ThemeColor = "#38bdf8",
            Position = "bottom-right",
            Language = "tr",
            IsAiEnabled = true,
            VersionNumber = 1,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        await _settingRepo.AddAsync(setting);
        await _unitOfWork.SaveChangesAsync();
    }
}

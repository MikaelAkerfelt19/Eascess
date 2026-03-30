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

        setting.ThemeColor = dto.ThemeColor;
        setting.Position = dto.Position;
        setting.Language = dto.Language;
        setting.IsAiEnabled = dto.IsAiEnabled;
        setting.UpdatedAt = DateTime.UtcNow;

        _settingRepo.Update(setting);
        await _unitOfWork.SaveChangesAsync();
        return true;
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

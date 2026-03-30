using Eascess_Application.DTOs;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class WidgetService : IWidgetService
{
    private readonly IRepository<Domain> _domainRepo;
    private readonly IRepository<WidgetSetting> _widgetSettingRepo;

    public WidgetService(IRepository<Domain> domainRepo, IRepository<WidgetSetting> widgetSettingRepo)
    {
        _domainRepo = domainRepo;
        _widgetSettingRepo = widgetSettingRepo;
    }

    public async Task<WidgetConfigDto?> GetConfigByLicenseKeyAsync(Guid licenseKey)
    {
        var domain = await _domainRepo.FirstOrDefaultAsync(d => d.LicenseKey == licenseKey && d.IsDeleted != true);
        if (domain is null)
            return null;

        var setting = await _widgetSettingRepo.FirstOrDefaultAsync(w => w.DomainId == domain.Id && w.IsActive);

        return new WidgetConfigDto
        {
            ThemeColor = setting?.ThemeColor ?? "#0056b3",
            Position = setting?.Position ?? "bottom-right",
            Language = setting?.Language ?? "tr",
            IsAiEnabled = setting?.IsAiEnabled ?? true,
            DomainUrl = domain.DomainUrl,
        };
    }
}

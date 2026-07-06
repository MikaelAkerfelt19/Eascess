using Eascess_Application.DTOs;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class WidgetService : IWidgetService
{
    private readonly IRepository<Domain> _domainRepo;
    private readonly IRepository<WidgetSetting> _widgetSettingRepo;
    private readonly IPlanService _planService;

    public WidgetService(IRepository<Domain> domainRepo, IRepository<WidgetSetting> widgetSettingRepo, IPlanService planService)
    {
        _domainRepo = domainRepo;
        _widgetSettingRepo = widgetSettingRepo;
        _planService = planService;
    }

    public async Task<WidgetConfigDto?> GetConfigByLicenseKeyAsync(Guid licenseKey)
    {
        var domain = await _domainRepo.FirstOrDefaultAsync(d => d.LicenseKey == licenseKey && d.IsDeleted != true);
        if (domain is null)
            return null;

        var setting = await _widgetSettingRepo.FirstOrDefaultAsync(w => w.DomainId == domain.Id && w.IsActive);

        // Widget özelleştirme Pro ve üzeri planlara özeldir. Plan kapsam dışına
        // düşen (ör. Pro'dan Ücretsiz'e inen) kullanıcının widget'ı, kayıtlı
        // özelleştirme dursa bile varsayılan görünümle servis edilir.
        var plan = await _planService.GetUserActivePlanAsync(domain.UserId);
        if (!plan.HasWidgetCustomization)
        {
            return new WidgetConfigDto
            {
                DomainUrl = domain.DomainUrl,
                IsAiEnabled = setting?.IsAiEnabled ?? true,
                // ThemeColor / Position / Language / LogoUrl / WidgetTitle /
                // PoweredByVisible → DTO varsayılanları
            };
        }

        return new WidgetConfigDto
        {
            ThemeColor = setting?.ThemeColor ?? "#0056b3",
            Position = setting?.Position ?? "bottom-right",
            Language = setting?.Language ?? "tr",
            IsAiEnabled = setting?.IsAiEnabled ?? true,
            DomainUrl = domain.DomainUrl,
            LogoUrl = setting?.LogoUrl,
            WidgetTitle = setting?.WidgetTitle,
            PoweredByVisible = setting?.PoweredByVisible ?? true,
        };
    }
}

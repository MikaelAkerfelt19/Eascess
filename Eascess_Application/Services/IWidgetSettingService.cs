using Eascess_Application.DTOs;

namespace Eascess_Application.Services;

public interface IWidgetSettingService
{
    Task<WidgetSettingDto?> GetByDomainAsync(int domainId, string userId);
    Task<bool> UpdateAsync(WidgetSettingDto dto, string userId);
    Task CreateDefaultAsync(int domainId);
}

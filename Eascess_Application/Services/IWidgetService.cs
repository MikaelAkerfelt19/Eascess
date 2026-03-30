using Eascess_Application.DTOs;

namespace Eascess_Application.Services;

public interface IWidgetService
{
    Task<WidgetConfigDto?> GetConfigByLicenseKeyAsync(Guid licenseKey);
}

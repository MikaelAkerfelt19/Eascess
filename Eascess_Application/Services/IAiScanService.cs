using Eascess_Application.DTOs;

namespace Eascess_Application.Services;

public interface IAiScanService
{
    Task<AltTextBatchResultDto> ProcessImagesAsync(Guid licenseKey, List<string> imageUrls, CancellationToken ct = default);
}

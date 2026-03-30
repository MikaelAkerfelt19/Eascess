using Eascess_Application.DTOs;

namespace Eascess_Application.Services;

public interface IScanReportService
{
    Task<IEnumerable<ScanReportListItemDto>> GetAllReportsAsync(string userId);
    Task<IEnumerable<ScanReportListItemDto>> GetReportsForDomainAsync(int domainId, string userId);
    Task<ScanReportDetailDto?> GetReportDetailAsync(int reportId, string userId);
    Task<int> GetTotalReportCountAsync(string userId);
}

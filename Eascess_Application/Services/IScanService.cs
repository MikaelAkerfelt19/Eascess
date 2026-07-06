using Eascess_Application.DTOs;

namespace Eascess_Application.Services;

public interface IScanService
{
    /// <summary>
    /// Belirtilen domain'in ana sayfasını WCAG 2.2'ye göre tarar,
    /// ScanReport ve ScanReportDetail kayıtlarını oluşturur.
    /// </summary>
    /// <param name="scanType">Rapora yazılan tetikleme türü: "Manual" veya "Auto".</param>
    Task<ScanResultDto> ScanDomainAsync(int domainId, string userId, string scanType = "Manual");
}

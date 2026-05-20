namespace Eascess_Application.Services;

public interface IMonthlyReportService
{
    /// <summary>
    /// Belirli bir domain için aylık erişilebilirlik raporu PDF'i üretir ve email ile gönderir.
    /// </summary>
    Task GenerateAndSendAsync(int domainId, int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Tüm aktif domain'ler için toplu rapor üretir (background job tarafından çağrılır).
    /// </summary>
    Task GenerateForAllDomainsAsync(int year, int month, CancellationToken ct = default);
}

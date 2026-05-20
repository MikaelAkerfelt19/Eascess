namespace Eascess_Application.Services;

public record PublicScanIssue(string Code, string Description, string Severity);

public record PublicScanResult(
    bool Success,
    string? ErrorMessage,
    int Score,
    int TotalIssues,
    int CriticalCount,
    int WarningCount,
    IReadOnlyList<PublicScanIssue> TopIssues);

public interface IPublicScanService
{
    /// <summary>
    /// Herhangi bir URL'i WCAG 2.2'ye göre tarar.
    /// Veritabanına kayıt yapmaz — anonim kullanım için.
    /// </summary>
    Task<PublicScanResult> ScanAsync(string url, CancellationToken ct = default);
}

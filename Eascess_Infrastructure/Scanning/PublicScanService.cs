using Eascess_Application.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Eascess_Infrastructure.Scanning;

public sealed class PublicScanService : IPublicScanService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PublicScanService> _logger;

    public PublicScanService(IHttpClientFactory httpClientFactory, ILogger<PublicScanService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PublicScanResult> ScanAsync(string url, CancellationToken ct = default)
    {
        // Normalize
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Fail("Geçersiz URL formatı.");

        string html;
        try
        {
            var client = _httpClientFactory.CreateClient("WcagScanner");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(12));
            html = await client.GetStringAsync(uri, cts.Token);
        }
        catch (Exception ex)
        {
            // Hata ayrıntısı yalnızca loglanır; istemciye iç ağ/istisna detayı sızdırılmaz
            _logger.LogWarning(ex, "Public scan fetch failed: {Url}", url);
            return Fail("Sayfaya erişilemedi. Adresin herkese açık ve doğru olduğundan emin olun.");
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var issues = WcagAnalyzer.Analyze(doc);

        int critical = issues.Count(i => i.Severity == "Critical");
        int warning  = issues.Count(i => i.Severity == "Warning");
        int score    = CalculateScore(issues);

        var topIssues = issues
            .OrderBy(i => i.Severity == "Critical" ? 0 : i.Severity == "Warning" ? 1 : 2)
            .Take(10)
            .Select(i => new PublicScanIssue(i.ErrorCode, i.ErrorDescription, i.Severity))
            .ToList();

        return new PublicScanResult(true, null, score, issues.Count, critical, warning, topIssues);
    }

    private static int CalculateScore(IReadOnlyList<WcagIssue> issues)
    {
        int deductions = issues.Sum(i => i.Severity switch
        {
            "Critical" => 10,
            "Warning"  => 4,
            _          => 1,
        });
        return Math.Max(0, 100 - deductions);
    }

    private static PublicScanResult Fail(string msg) =>
        new(false, msg, 0, 0, 0, 0, Array.Empty<PublicScanIssue>());
}

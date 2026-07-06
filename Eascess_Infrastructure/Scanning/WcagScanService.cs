using Eascess_Application.DTOs;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Eascess_Infrastructure.Scanning;

public sealed class WcagScanService : IScanService
{
    private readonly IRepository<Domain> _domainRepo;
    private readonly IRepository<Page> _pageRepo;
    private readonly IRepository<ScanReport> _reportRepo;
    private readonly IRepository<ScanReportDetail> _detailRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WcagScanService> _logger;

    public WcagScanService(
        IRepository<Domain> domainRepo,
        IRepository<Page> pageRepo,
        IRepository<ScanReport> reportRepo,
        IRepository<ScanReportDetail> detailRepo,
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        ILogger<WcagScanService> logger)
    {
        _domainRepo = domainRepo;
        _pageRepo = pageRepo;
        _reportRepo = reportRepo;
        _detailRepo = detailRepo;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ScanResultDto> ScanDomainAsync(int domainId, string userId, string scanType = "Manual")
    {
        // ── 1. Domain doğrula ─────────────────────────────────────────────
        var domain = await _domainRepo.FirstOrDefaultAsync(
            d => d.Id == domainId && d.UserId == userId && d.IsDeleted != true);

        if (domain is null)
            return Fail(domainId, "Domain bulunamadı veya erişim reddedildi.");

        var pageUrl = "https://" + domain.DomainUrl;

        // ── 2. HTML çek ───────────────────────────────────────────────────
        string html;
        try
        {
            var client = _httpClientFactory.CreateClient("WcagScanner");
            html = await client.GetStringAsync(pageUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tarama sırasında sayfa çekilemedi: {Url}", pageUrl);
            return Fail(domainId, $"Sayfa erişilemedi: {ex.Message}");
        }

        // ── 3. HTML parse & analiz ─────────────────────────────────────────
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var issues = WcagAnalyzer.Analyze(doc);

        // ── 4. WCAG skoru hesapla ─────────────────────────────────────────
        int score = CalculateScore(issues);
        int errorCount = issues.Count(i => i.Severity is "Critical" or "Warning");

        // ── 5. Page kaydı (upsert) ────────────────────────────────────────
        var page = await _pageRepo.FirstOrDefaultAsync(
            p => p.DomainId == domainId && p.PageUrl == pageUrl && !p.IsDeleted);

        if (page is null)
        {
            page = new Page
            {
                DomainId = domainId,
                PageUrl = pageUrl,
                LastScannedAt = DateTime.UtcNow,
                IsDeleted = false,
            };
            await _pageRepo.AddAsync(page);
        }
        else
        {
            page.LastScannedAt = DateTime.UtcNow;
            _pageRepo.Update(page);
        }

        await _unitOfWork.SaveChangesAsync(); // page.Id'yi almak için önce kaydet

        // ── 6. ScanReport oluştur ─────────────────────────────────────────
        var report = new ScanReport
        {
            DomainId = domainId,
            ScanType = scanType,
            WcagScore = score,
            ErrorCount = errorCount,
            ScanDate = DateTime.UtcNow,
        };
        await _reportRepo.AddAsync(report);
        await _unitOfWork.SaveChangesAsync(); // report.Id almak için

        // ── 7. ScanReportDetails kaydet ───────────────────────────────────
        foreach (var issue in issues)
        {
            await _detailRepo.AddAsync(new ScanReportDetail
            {
                ScanReportId     = report.Id,
                PageId           = page.Id,
                ElementSelector  = issue.ElementSelector,
                ErrorCode        = issue.ErrorCode,
                ErrorDescription = issue.ErrorDescription,
                WcagLevel        = issue.WcagLevel,
                Severity         = issue.Severity,
            });
        }

        if (issues.Any())
            await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Tarama tamamlandı — Domain:{DomainId} Skor:{Score} Sorun:{Count}",
            domainId, score, issues.Count);

        return new ScanResultDto
        {
            Success = true,
            DomainId = domainId,
            ReportId = report.Id,
            WcagScore = score,
            ErrorCount = errorCount,
        };
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────

    private static int CalculateScore(IReadOnlyList<WcagIssue> issues)
    {
        int deduct = 0;
        deduct += Math.Min(issues.Count(i => i.Severity == "Critical") * 12, 60);
        deduct += Math.Min(issues.Count(i => i.Severity == "Warning")  * 5,  30);
        deduct += Math.Min(issues.Count(i => i.Severity == "Info")     * 1,  10);
        return Math.Max(0, 100 - deduct);
    }

    private static ScanResultDto Fail(int domainId, string message) => new()
    {
        Success = false,
        DomainId = domainId,
        ErrorMessage = message,
    };
}

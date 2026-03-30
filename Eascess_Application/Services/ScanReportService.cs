using Eascess_Application.DTOs;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class ScanReportService : IScanReportService
{
    private readonly IRepository<ScanReport> _scanReportRepo;
    private readonly IRepository<ScanReportDetail> _detailRepo;
    private readonly IRepository<Domain> _domainRepo;
    private readonly IRepository<Page> _pageRepo;

    public ScanReportService(
        IRepository<ScanReport> scanReportRepo,
        IRepository<ScanReportDetail> detailRepo,
        IRepository<Domain> domainRepo,
        IRepository<Page> pageRepo)
    {
        _scanReportRepo = scanReportRepo;
        _detailRepo = detailRepo;
        _domainRepo = domainRepo;
        _pageRepo = pageRepo;
    }

    public async Task<IEnumerable<ScanReportListItemDto>> GetAllReportsAsync(string userId)
    {
        var domains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);
        var domainDict = domains.ToDictionary(d => d.Id, d => d.DomainUrl);
        var domainIds = domainDict.Keys.ToList();

        var reports = await _scanReportRepo.FindAsync(r => domainIds.Contains(r.DomainId));

        return reports
            .OrderByDescending(r => r.ScanDate)
            .Select(r => new ScanReportListItemDto
            {
                Id = r.Id,
                DomainId = r.DomainId,
                DomainUrl = domainDict.GetValueOrDefault(r.DomainId, ""),
                ScanType = r.ScanType ?? "Manual",
                WcagScore = r.WcagScore,
                ErrorCount = r.ErrorCount,
                ScanDate = r.ScanDate,
            });
    }

    public async Task<IEnumerable<ScanReportListItemDto>> GetReportsForDomainAsync(int domainId, string userId)
    {
        var domain = await _domainRepo.FirstOrDefaultAsync(d => d.Id == domainId && d.UserId == userId && d.IsDeleted != true);
        if (domain is null)
            return Enumerable.Empty<ScanReportListItemDto>();

        var reports = await _scanReportRepo.FindAsync(r => r.DomainId == domainId);

        return reports
            .OrderByDescending(r => r.ScanDate)
            .Select(r => new ScanReportListItemDto
            {
                Id = r.Id,
                DomainId = r.DomainId,
                DomainUrl = domain.DomainUrl,
                ScanType = r.ScanType ?? "Manual",
                WcagScore = r.WcagScore,
                ErrorCount = r.ErrorCount,
                ScanDate = r.ScanDate,
            });
    }

    public async Task<ScanReportDetailDto?> GetReportDetailAsync(int reportId, string userId)
    {
        var report = await _scanReportRepo.GetByIdAsync(reportId);
        if (report is null)
            return null;

        var domain = await _domainRepo.FirstOrDefaultAsync(d => d.Id == report.DomainId && d.UserId == userId && d.IsDeleted != true);
        if (domain is null)
            return null;

        var rawDetails = await _detailRepo.FindAsync(d => d.ScanReportId == reportId);
        var pageIds = rawDetails.Select(d => d.PageId).Distinct().ToList();
        var pages = await _pageRepo.FindAsync(p => pageIds.Contains(p.Id));
        var pageDict = pages.ToDictionary(p => p.Id, p => p.PageUrl);

        var issues = rawDetails.Select(d => new ScanIssueDto
        {
            Id = d.Id,
            PageUrl = pageDict.GetValueOrDefault(d.PageId, ""),
            ElementSelector = d.ElementSelector,
            ErrorCode = d.ErrorCode,
            ErrorDescription = d.ErrorDescription,
            WcagLevel = d.WcagLevel,
            Severity = d.Severity,
        }).ToList();

        return new ScanReportDetailDto
        {
            Id = report.Id,
            DomainId = report.DomainId,
            DomainUrl = domain.DomainUrl,
            ScanType = report.ScanType ?? "Manual",
            WcagScore = report.WcagScore,
            ErrorCount = report.ErrorCount,
            ScanDate = report.ScanDate,
            Issues = issues,
        };
    }

    public async Task<int> GetTotalReportCountAsync(string userId)
    {
        var domains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);
        var domainIds = domains.Select(d => d.Id).ToList();

        var allReports = await _scanReportRepo.FindAsync(r => domainIds.Contains(r.DomainId));
        return allReports.Count();
    }
}

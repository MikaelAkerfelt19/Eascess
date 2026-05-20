using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IScanReportService _scanReportService;
    private readonly IWidgetAnalyticsService _analytics;
    private readonly IRepository<Domain> _domainRepo;
    private readonly UserManager<AppUser> _userManager;

    public ReportsController(
        IScanReportService scanReportService,
        IWidgetAnalyticsService analytics,
        IRepository<Domain> domainRepo,
        UserManager<AppUser> userManager)
    {
        _scanReportService = scanReportService;
        _analytics         = analytics;
        _domainRepo        = domainRepo;
        _userManager       = userManager;
    }

    // GET /Reports — tüm sitelerin raporları
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var reports = await _scanReportService.GetAllReportsAsync(userId);
        return View(reports);
    }

    // GET /Reports/Domain/5 — belirli bir sitenin raporları
    public async Task<IActionResult> Domain(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var reports = await _scanReportService.GetReportsForDomainAsync(id, userId);
        ViewBag.DomainId = id;
        return View(reports);
    }

    // GET /Reports/Analytics/5 — domain bazlı widget kullanım analitikleri
    public async Task<IActionResult> Analytics(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var domain = await _domainRepo.FirstOrDefaultAsync(
            d => d.Id == id && d.UserId == userId && d.IsDeleted != true);

        if (domain is null) return NotFound();

        var daily   = await _analytics.GetDailyOpenCountsAsync(id);
        var top     = await _analytics.GetTopFeaturesAsync(id);
        var monthly = await _analytics.GetMonthlyStatsAsync(id);

        ViewBag.Domain  = domain;
        ViewBag.Daily   = daily;
        ViewBag.Top     = top;
        ViewBag.Monthly = monthly;
        return View();
    }

    // GET /Reports/Detail/5
    public async Task<IActionResult> Detail(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var report = await _scanReportService.GetReportDetailAsync(id, userId);

        if (report is null)
            return NotFound();

        return View(report);
    }
}

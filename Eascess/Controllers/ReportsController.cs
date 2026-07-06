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
    private readonly IPlanService _planService;

    public ReportsController(
        IScanReportService scanReportService,
        IWidgetAnalyticsService analytics,
        IRepository<Domain> domainRepo,
        UserManager<AppUser> userManager,
        IPlanService planService)
    {
        _scanReportService = scanReportService;
        _analytics         = analytics;
        _domainRepo        = domainRepo;
        _userManager       = userManager;
        _planService       = planService;
    }

    // Detaylı raporlar Pro ve üzeri planlara özeldir (fiyatlandırma vaadi)
    private async Task<bool> PlanAllowsReportsAsync(string userId)
    {
        var plan = await _planService.GetUserActivePlanAsync(userId);
        return plan.HasDetailedReports;
    }

    private IActionResult RedirectToPlans()
    {
        TempData["PlanWarning"] =
            "Detaylı raporlar ve analitikler Pro ve üzeri planlarda kullanılabilir. Devam etmek için planınızı yükseltin.";
        return RedirectToAction("Index", "Subscription");
    }

    // GET /Reports — tüm sitelerin raporları
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;

        if (!await PlanAllowsReportsAsync(userId))
            return RedirectToPlans();

        var reports = await _scanReportService.GetAllReportsAsync(userId);
        return View(reports);
    }

    // GET /Reports/Domain/5 — belirli bir sitenin raporları
    public async Task<IActionResult> Domain(int id)
    {
        var userId = _userManager.GetUserId(User)!;

        if (!await PlanAllowsReportsAsync(userId))
            return RedirectToPlans();

        var reports = await _scanReportService.GetReportsForDomainAsync(id, userId);
        ViewBag.DomainId = id;
        return View(reports);
    }

    // GET /Reports/Analytics/5 — domain bazlı widget kullanım analitikleri
    public async Task<IActionResult> Analytics(int id)
    {
        var userId = _userManager.GetUserId(User)!;

        if (!await PlanAllowsReportsAsync(userId))
            return RedirectToPlans();

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

        if (!await PlanAllowsReportsAsync(userId))
            return RedirectToPlans();

        var report = await _scanReportService.GetReportDetailAsync(id, userId);

        if (report is null)
            return NotFound();

        return View(report);
    }
}

using Eascess.Models;
using Eascess_Application.DTOs;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IRepository<Domain> _domainRepo;
    private readonly IScanReportService _scanReportService;
    private readonly IPlanService _planService;
    private readonly IWidgetSettingService _widgetSettingService;
    private readonly UserManager<AppUser> _userManager;

    public DashboardController(
        IRepository<Domain> domainRepo,
        IScanReportService scanReportService,
        IPlanService planService,
        IWidgetSettingService widgetSettingService,
        UserManager<AppUser> userManager)
    {
        _domainRepo = domainRepo;
        _scanReportService = scanReportService;
        _planService = planService;
        _widgetSettingService = widgetSettingService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var domains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);
        var domainList = domains.OrderByDescending(d => d.CreatedAt).ToList();

        var vm = domainList.Select(d => new DomainListItemViewModel
        {
            Id = d.Id,
            DomainUrl = d.DomainUrl,
            LicenseKey = d.LicenseKey,
            IsVerified = d.IsVerified ?? false,
            CreatedAt = d.CreatedAt,
        }).ToList();

        ViewBag.TotalReportCount = await _scanReportService.GetTotalReportCountAsync(userId);

        var plan = await _planService.GetUserActivePlanAsync(userId);
        ViewBag.PlanName = plan.Name;
        ViewBag.MaxDomains = plan.MaxDomains;
        ViewBag.MonthlyAiQuota = plan.MonthlyAiQuota;
        ViewBag.AiUsedThisMonth = await _planService.GetMonthlyAiUsageAsync(userId);
        ViewBag.DomainCount = domainList.Count;

        if (domainList.Any())
            ViewBag.QuickWidgetSettings = await _widgetSettingService.GetByDomainAsync(domainList.First().Id, userId);

        var appUser = await _userManager.GetUserAsync(User);
        // Deneme sayacı yalnızca denemesi HÂLÂ süren kullanıcıya gösterilir.
        // Bir plana geçildiğinde CheckoutService denemeyi kapattığı için
        // IsTrialActive false olur; üst kademeye elle taşınanlar da elenir.
        if (appUser?.IsTrialActive == true && !plan.IsAboveTrialTier)
        {
            var daysLeft = (int)Math.Ceiling((appUser.TrialEndsAt.Value - DateTime.UtcNow).TotalDays);
            ViewBag.TrialDaysLeft = Math.Max(0, daysLeft);
        }

        return View(vm);
    }
}

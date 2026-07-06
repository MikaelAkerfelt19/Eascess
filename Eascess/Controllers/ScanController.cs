using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

[Authorize]
public class ScanController : Controller
{
    private readonly IScanService _scanService;
    private readonly UserManager<AppUser> _userManager;
    private readonly IPlanService _planService;

    public ScanController(IScanService scanService, UserManager<AppUser> userManager, IPlanService planService)
    {
        _scanService = scanService;
        _userManager = userManager;
        _planService = planService;
    }

    /// <summary>
    /// Belirtilen domain'i manuel olarak tarar.
    /// POST /Scan/Start/5
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var result = await _scanService.ScanDomainAsync(id, userId);

        // WCAG denetimi tüm planlarda vaat edilir; detaylı rapor sayfaları ise
        // Pro ve üzeri planlara özeldir. Ücretsiz kullanıcıya özet Dashboard'da gösterilir.
        var plan = await _planService.GetUserActivePlanAsync(userId);

        if (!result.Success)
        {
            if (!plan.HasDetailedReports)
            {
                TempData["Success"] = result.ErrorMessage ?? "Tarama sırasında bir hata oluştu.";
                return RedirectToAction("Index", "Dashboard");
            }

            TempData["ScanError"] = result.ErrorMessage ?? "Tarama sırasında bir hata oluştu.";
            return RedirectToAction("Domain", "Reports", new { id });
        }

        var summary = $"Tarama tamamlandı — WCAG Skoru: {result.WcagScore}/100, {result.ErrorCount} sorun tespit edildi.";

        if (!plan.HasDetailedReports)
        {
            TempData["Success"] = summary + " Sayfa bazlı detaylı raporlar için planınızı yükseltebilirsiniz.";
            return RedirectToAction("Index", "Dashboard");
        }

        TempData["ScanSuccess"] = summary;
        return RedirectToAction("Detail", "Reports", new { id = result.ReportId });
    }
}

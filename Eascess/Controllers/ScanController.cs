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

    public ScanController(IScanService scanService, UserManager<AppUser> userManager)
    {
        _scanService = scanService;
        _userManager = userManager;
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

        if (!result.Success)
        {
            TempData["ScanError"] = result.ErrorMessage ?? "Tarama sırasında bir hata oluştu.";
            return RedirectToAction("Domain", "Reports", new { id });
        }

        TempData["ScanSuccess"] = $"Tarama tamamlandı — WCAG Skoru: {result.WcagScore}/100, {result.ErrorCount} sorun tespit edildi.";
        return RedirectToAction("Detail", "Reports", new { id = result.ReportId });
    }
}

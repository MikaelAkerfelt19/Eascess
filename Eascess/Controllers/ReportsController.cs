using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IScanReportService _scanReportService;
    private readonly UserManager<AppUser> _userManager;

    public ReportsController(IScanReportService scanReportService, UserManager<AppUser> userManager)
    {
        _scanReportService = scanReportService;
        _userManager = userManager;
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

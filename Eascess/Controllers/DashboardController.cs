using Eascess.Models;
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
    private readonly UserManager<AppUser> _userManager;

    public DashboardController(
        IRepository<Domain> domainRepo,
        IScanReportService scanReportService,
        UserManager<AppUser> userManager)
    {
        _domainRepo = domainRepo;
        _scanReportService = scanReportService;
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
        return View(vm);
    }
}

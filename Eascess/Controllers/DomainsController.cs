using Eascess.Models;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

[Authorize]
public class DomainsController : Controller
{
    private readonly IRepository<Domain> _domainRepo;
    private readonly IWidgetSettingService _widgetSettingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public DomainsController(
        IRepository<Domain> domainRepo,
        IWidgetSettingService widgetSettingService,
        IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager)
    {
        _domainRepo = domainRepo;
        _widgetSettingService = widgetSettingService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    // ── Domain Listesi ────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var domains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);

        var viewModel = domains
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DomainListItemViewModel
            {
                Id = d.Id,
                DomainUrl = d.DomainUrl,
                LicenseKey = d.LicenseKey,
                IsVerified = d.IsVerified ?? false,
                CreatedAt = d.CreatedAt,
            });

        return View(viewModel);
    }

    // ── Domain Ekle ───────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Add() => View(new AddDomainViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddDomainViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = _userManager.GetUserId(User)!;
        var normalizedUrl = NormalizeUrl(model.DomainUrl);

        var existing = await _domainRepo.FirstOrDefaultAsync(
            d => d.DomainUrl == normalizedUrl && d.IsDeleted != true);

        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.DomainUrl), "Bu domain zaten sisteme kayıtlı.");
            return View(model);
        }

        var domain = new Domain
        {
            UserId = userId,
            DomainUrl = normalizedUrl,
            LicenseKey = Guid.NewGuid(),
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        };

        await _domainRepo.AddAsync(domain);
        await _unitOfWork.SaveChangesAsync();

        // Domain için varsayılan widget ayarlarını oluştur
        await _widgetSettingService.CreateDefaultAsync(domain.Id);

        TempData["Success"] = $"{normalizedUrl} başarıyla eklendi. Script etiketini kopyalayıp sitenize yapıştırın.";
        return RedirectToAction(nameof(Script), new { id = domain.Id });
    }

    // ── Domain Sil ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var domain = await _domainRepo.GetByIdAsync(id);

        if (domain is null || domain.UserId != userId)
            return NotFound();

        domain.IsDeleted = true;
        domain.DeletedAt = DateTime.UtcNow;
        _domainRepo.Update(domain);
        await _unitOfWork.SaveChangesAsync();

        TempData["Success"] = "Domain silindi.";
        return RedirectToAction(nameof(Index));
    }

    // ── Script Tag Sayfası ────────────────────────────────────────────────────

    public async Task<IActionResult> Script(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var domain = await _domainRepo.GetByIdAsync(id);

        if (domain is null || domain.UserId != userId || domain.IsDeleted == true)
            return NotFound();

        var vm = new DomainListItemViewModel
        {
            Id = domain.Id,
            DomainUrl = domain.DomainUrl,
            LicenseKey = domain.LicenseKey,
            IsVerified = domain.IsVerified ?? false,
            CreatedAt = domain.CreatedAt,
        };

        return View(vm);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeUrl(string url)
    {
        url = url.Trim().ToLowerInvariant();
        if (url.StartsWith("https://")) url = url[8..];
        else if (url.StartsWith("http://")) url = url[7..];
        return url.TrimEnd('/');
    }
}

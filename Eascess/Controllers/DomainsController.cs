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
    private readonly IRepository<WidgetSetting> _widgetSettingRepo;
    private readonly IWidgetSettingService _widgetSettingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;
    private readonly IPlanService _planService;

    public DomainsController(
        IRepository<Domain> domainRepo,
        IRepository<WidgetSetting> widgetSettingRepo,
        IWidgetSettingService widgetSettingService,
        IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager,
        IPlanService planService)
    {
        _domainRepo = domainRepo;
        _widgetSettingRepo = widgetSettingRepo;
        _widgetSettingService = widgetSettingService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _planService = planService;
    }

    // ── Domain Listesi ────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var domains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var viewModel = domains
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DomainListItemViewModel
            {
                Id = d.Id,
                DomainUrl = d.DomainUrl,
                LicenseKey = d.LicenseKey,
                IsVerified = d.IsVerified ?? false,
                CreatedAt = d.CreatedAt,
                AppBaseUrl = baseUrl,
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

        // Plan limiti kontrolü
        var plan = await _planService.GetUserActivePlanAsync(userId);
        var userDomains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);
        if (userDomains.Count() >= plan.MaxDomains)
        {
            ModelState.AddModelError(string.Empty,
                $"Planınız en fazla {plan.MaxDomains} domain desteklemektedir. Daha fazla eklemek için planınızı yükseltin.");
            return View(model);
        }

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

        // İlişkili widget ayarlarını deaktive et
        var widgetSettings = await _widgetSettingRepo.FindAsync(w => w.DomainId == id);
        foreach (var ws in widgetSettings)
        {
            ws.IsActive = false;
            _widgetSettingRepo.Update(ws);
        }

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
            AppBaseUrl = $"{Request.Scheme}://{Request.Host}",
        };

        ViewBag.WidgetSettings = await _widgetSettingService.GetByDomainAsync(id, userId);

        return View(vm);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeUrl(string url)
    {
        url = url.Trim().ToLowerInvariant();

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "https://" + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url.Replace("https://", "").Replace("http://", "").TrimEnd('/');

        // Sadece host'u al — port, path, query yok
        return uri.Host.TrimEnd('/');
    }
}

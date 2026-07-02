using System.Collections.Generic;
using System.IO;
using Eascess_Application.DTOs;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

[Authorize]
public class WidgetSettingsController : Controller
{
    private readonly IWidgetSettingService _widgetSettingService;
    private readonly IRepository<Domain> _domainRepo;
    private readonly UserManager<AppUser> _userManager;
    private readonly IWebHostEnvironment _env;

    private static readonly HashSet<string> _allowedMime = new(StringComparer.OrdinalIgnoreCase)
        { "image/png", "image/jpeg", "image/svg+xml", "image/webp" };

    // Uzantı, istemcinin gönderdiği dosya adına güvenilmeden MIME türünden türetilir;
    // aksi halde ".html" gibi bir uzantı static files üzerinden stored-XSS'e yol açar.
    private static readonly Dictionary<string, string> _extByMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/svg+xml"] = ".svg",
        ["image/webp"] = ".webp",
    };

    private static readonly HashSet<string> _allowedExt = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".svg", ".webp" };

    public WidgetSettingsController(IWidgetSettingService widgetSettingService, IRepository<Domain> domainRepo, UserManager<AppUser> userManager, IWebHostEnvironment env)
    {
        _widgetSettingService = widgetSettingService;
        _domainRepo = domainRepo;
        _userManager = userManager;
        _env = env;
    }

    // GET /WidgetSettings/Index/5
    public async Task<IActionResult> Index(int id)
    {
        var userId = _userManager.GetUserId(User)!;

        if (id == 0)
        {
            var first = (await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true))
                .OrderBy(d => d.CreatedAt)
                .FirstOrDefault();

            if (first is null) return RedirectToAction("Index", "Domains");

            return RedirectToAction(nameof(Index), new { id = first.Id });
        }

        var dto = await _widgetSettingService.GetByDomainAsync(id, userId);

        if (dto is null) return NotFound();

        return View(dto);
    }

    // POST /WidgetSettings/UploadLogo
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return Json(new { error = "Dosya boş." });

        if (file.Length > 2 * 1024 * 1024)
            return Json(new { error = "Dosya 2 MB'dan büyük olamaz." });

        if (!_allowedMime.Contains(file.ContentType))
            return Json(new { error = "Geçersiz dosya türü." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExt.Contains(ext))
            ext = _extByMime[file.ContentType];
        var userId = _userManager.GetUserId(User)!;
        var folder = Path.Combine(_env.WebRootPath, "uploads", "logos", userId);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(folder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"/uploads/logos/{userId}/{fileName}";
        return Json(new { url });
    }

    // POST /WidgetSettings/Update
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(WidgetSettingDto dto, string? returnUrl = null)
    {
        var userId = _userManager.GetUserId(User)!;

        if (!ModelState.IsValid)
            return View("Index", dto);

        // Eski logo URL'sini al (silinecek mi diye kontrol için)
        var current = await _widgetSettingService.GetByDomainAsync(dto.DomainId, userId);
        var oldLogoUrl = current?.LogoUrl;

        var success = await _widgetSettingService.UpdateAsync(dto, userId);

        if (!success) return NotFound();

        // Eski yerel logo dosyasını sil
        var newLogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim();
        if (oldLogoUrl != newLogoUrl
            && oldLogoUrl is not null
            && oldLogoUrl.StartsWith("/uploads/logos/", StringComparison.OrdinalIgnoreCase))
        {
            // "/uploads/logos/../../x" gibi URL'lerin klasör dışına çıkmasını engelle
            var logosRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "logos"));
            var filePath = Path.GetFullPath(Path.Combine(_env.WebRootPath,
                oldLogoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

            if (filePath.StartsWith(logosRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        TempData["Success"] = "Widget ayarları güncellendi.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index), new { id = dto.DomainId });
    }
}

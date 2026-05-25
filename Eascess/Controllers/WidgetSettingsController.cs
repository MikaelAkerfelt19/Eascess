using Eascess_Application.DTOs;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

[Authorize]
public class WidgetSettingsController : Controller
{
    private readonly IWidgetSettingService _widgetSettingService;
    private readonly UserManager<AppUser> _userManager;

    public WidgetSettingsController(IWidgetSettingService widgetSettingService, UserManager<AppUser> userManager)
    {
        _widgetSettingService = widgetSettingService;
        _userManager = userManager;
    }

    // GET /WidgetSettings/Index/5
    public async Task<IActionResult> Index(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var dto = await _widgetSettingService.GetByDomainAsync(id, userId);

        if (dto is null) return NotFound();

        return View(dto);
    }

    // POST /WidgetSettings/Update
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(WidgetSettingDto dto, string? returnUrl = null)
    {
        var userId = _userManager.GetUserId(User)!;

        if (!ModelState.IsValid)
            return View("Index", dto);

        var success = await _widgetSettingService.UpdateAsync(dto, userId);

        if (!success) return NotFound();

        TempData["Success"] = "Widget ayarları güncellendi.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index), new { id = dto.DomainId });
    }
}

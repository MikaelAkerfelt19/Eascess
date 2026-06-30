using Eascess.Models;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IRepository<UserSubscription> _subscriptionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IRepository<UserSubscription> subscriptionRepo,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _subscriptionRepo = subscriptionRepo;
        _unitOfWork = unitOfWork;
    }

    // ── Login ────────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, "E-posta adresi veya şifre hatalı.");
        return View(model);
    }

    // ── Register ─────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Register(string? @ref = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        if (@ref == "ai-alt-text")
            ViewData["PromoMessage"] = "AI Alt Metin özelliğini denemek için kayıt olun";

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            TrialStartedAt = now,
            TrialEndsAt = now.AddDays(14),
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Deneme süresince Pro plan (id=2); deneme bitince Ücretsiz'e (id=1) düşer
            await _subscriptionRepo.AddAsync(new UserSubscription
            {
                UserId = user.Id,
                PlanId = 2, // Pro plan — 14 günlük deneme
                StartDate = now,
                EndDate = now.AddDays(14),
                IsActive = true,
                AutoRenew = false,
                IsDeleted = false,
            });
            // Deneme sonrası kalıcı Ücretsiz plan.
            // IsActive=true ama StartDate=now+14 olduğu için deneme süresince henüz
            // "geçerli" sayılmaz; Pro deneme bitince otomatik olarak öne çıkar.
            await _subscriptionRepo.AddAsync(new UserSubscription
            {
                UserId = user.Id,
                PlanId = 1, // Ücretsiz plan
                StartDate = now.AddDays(14),
                EndDate = now.AddYears(100),
                IsActive = true,
                AutoRenew = false,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account");
    }
}

using Eascess.Models;
using Eascess_Domain.Constants;
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
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IRepository<UserSubscription> subscriptionRepo,
        IUnitOfWork unitOfWork,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _subscriptionRepo = subscriptionRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

        // lockoutOnFailure: brute-force koruması — ardışık hatalı denemede hesap geçici kilitlenir
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Çok fazla hatalı deneme yapıldı. Hesabınız geçici olarak kilitlendi, lütfen birkaç dakika sonra tekrar deneyin.");
            return View(model);
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
        // Deneme gün bazlıdır ve 14. günün sonunda gece 00:00 UTC'de biter
        var trialEnd = TrialPolicy.TrialEndUtc(now);
        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            TrialStartedAt = now,
            TrialEndsAt = trialEnd,
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Abonelik satırları oluşturulamazsa kayıt akışı yine tamamlanır;
            // PlanService abonelik bulamadığında Ücretsiz plana düşer.
            try
            {
                // Deneme süresince Pro plan; deneme bitince Ücretsiz'e düşer
                await _subscriptionRepo.AddAsync(new UserSubscription
                {
                    UserId = user.Id,
                    PlanId = TrialPolicy.TrialPlanId, // 14. günün sonunda 00:00 UTC'de biter
                    StartDate = now,
                    EndDate = trialEnd,
                    IsActive = true,
                    AutoRenew = false,
                    IsDeleted = false,
                });
                // Deneme sonrası kalıcı Ücretsiz plan.
                // IsActive=true ama StartDate ileri tarihli olduğu için deneme süresince
                // henüz "geçerli" sayılmaz; Pro deneme bitince otomatik olarak öne çıkar.
                await _subscriptionRepo.AddAsync(new UserSubscription
                {
                    UserId = user.Id,
                    PlanId = PlanIds.Free,
                    StartDate = trialEnd,
                    EndDate = now.AddYears(100),
                    IsActive = true,
                    AutoRenew = false,
                    IsDeleted = false,
                });
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kayıt sırasında deneme aboneliği oluşturulamadı. UserId={UserId}", user.Id);
            }

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

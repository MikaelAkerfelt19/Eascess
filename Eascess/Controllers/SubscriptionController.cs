using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers;

[Authorize]
public class SubscriptionController : Controller
{
    private readonly IPlanService _planService;
    private readonly IRepository<Domain> _domainRepo;
    private readonly UserManager<AppUser> _userManager;

    public SubscriptionController(
        IPlanService planService,
        IRepository<Domain> domainRepo,
        UserManager<AppUser> userManager)
    {
        _planService = planService;
        _domainRepo = domainRepo;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;

        var currentPlan = await _planService.GetUserActivePlanAsync(userId);
        var allPlans = await _planService.GetAllActivePlansAsync();
        var aiUsed = await _planService.GetMonthlyAiUsageAsync(userId);
        var domains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);
        var user = await _userManager.GetUserAsync(User);

        ViewBag.CurrentPlan = currentPlan;
        ViewBag.AllPlans = allPlans;
        ViewBag.AiUsed = aiUsed;
        ViewBag.DomainCount = domains.Count();
        ViewBag.PlanName = currentPlan.Name;
        ViewBag.TrialEndsAt = user?.IsTrialActive == true ? user.TrialEndsAt : null;

        return View();
    }
}

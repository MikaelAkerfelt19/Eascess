using System.Security.Claims;
using Eascess_Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.ViewComponents;

/// <summary>
/// Yan menü alt bilgisinde kullanıcının aktif plan adını gösterir.
/// ViewBag.PlanName'e bağımlılığı kaldırır — her sayfada doğru plan görünür.
/// </summary>
public class UserPlanNameViewComponent : ViewComponent
{
    private readonly IPlanService _planService;

    public UserPlanNameViewComponent(IPlanService planService)
    {
        _planService = planService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = (UserClaimsPrincipal as ClaimsPrincipal)?
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Content("Ücretsiz");

        var plan = await _planService.GetUserActivePlanAsync(userId);
        return Content(plan.Name);
    }
}

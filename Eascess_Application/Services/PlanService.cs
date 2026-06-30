using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class PlanService : IPlanService
{
    private const int FreePlanId = 1;

    private readonly IRepository<UserSubscription> _subscriptionRepo;
    private readonly IRepository<Plan> _planRepo;
    private readonly IRepository<AiUsageLog> _usageLogRepo;
    private readonly IRepository<Domain> _domainRepo;

    public PlanService(
        IRepository<UserSubscription> subscriptionRepo,
        IRepository<Plan> planRepo,
        IRepository<AiUsageLog> usageLogRepo,
        IRepository<Domain> domainRepo)
    {
        _subscriptionRepo = subscriptionRepo;
        _planRepo = planRepo;
        _usageLogRepo = usageLogRepo;
        _domainRepo = domainRepo;
    }

    public async Task<Plan> GetUserActivePlanAsync(string userId)
    {
        var now = DateTime.UtcNow;

        // Şu an geçerli (başlamış ve bitmemiş) aktif abonelikler içinde en yüksek
        // kademeyi seç. Deneme süresince Pro (PlanId=2), deneme bitince Ücretsiz
        // (PlanId=1) abonelik öne çıkar — yüksek PlanId daha üst kademe demektir.
        var subs = await _subscriptionRepo.FindAsync(
            s => s.UserId == userId && s.IsActive && !s.IsDeleted
                 && s.StartDate <= now && s.EndDate >= now);

        var planId = subs.OrderByDescending(s => s.PlanId)
                         .Select(s => (int?)s.PlanId)
                         .FirstOrDefault() ?? FreePlanId;

        var plan = await _planRepo.GetByIdAsync(planId);

        // Fallback: plan bulunamazsa varsayılan değerler döndür
        return plan ?? new Plan { Id = FreePlanId, Name = "Ücretsiz", MaxDomains = 1, MonthlyAiQuota = 50 };
    }

    public async Task<int> GetMonthlyAiUsageAsync(string userId)
    {
        var userDomains = await _domainRepo.FindAsync(
            d => d.UserId == userId && d.IsDeleted != true);

        if (!userDomains.Any())
            return 0;

        var domainIds = userDomains.Select(d => d.Id).ToHashSet();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var logs = await _usageLogRepo.FindAsync(
            l => domainIds.Contains(l.DomainId) && l.RequestDate >= monthStart);

        return logs.Count();
    }

    public async Task<IReadOnlyList<Plan>> GetAllActivePlansAsync()
    {
        var plans = await _planRepo.FindAsync(p => p.IsActive && !p.IsDeleted);
        return plans.OrderBy(p => p.MonthlyPrice).ToList();
    }
}

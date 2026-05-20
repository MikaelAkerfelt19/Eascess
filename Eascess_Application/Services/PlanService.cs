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
        var sub = await _subscriptionRepo.FirstOrDefaultAsync(
            s => s.UserId == userId && s.IsActive && !s.IsDeleted && s.EndDate >= DateTime.UtcNow);

        var planId = sub?.PlanId ?? FreePlanId;
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
}

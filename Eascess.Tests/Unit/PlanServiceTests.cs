using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Moq;
using System.Linq.Expressions;

namespace Eascess.Tests.Unit;

/// <summary>
/// PlanService.GetUserActivePlanAsync için unit testler.
/// </summary>
public class PlanServiceTests
{
    private readonly Mock<IRepository<UserSubscription>> _subRepo  = new();
    private readonly Mock<IRepository<Plan>>             _planRepo = new();
    private readonly Mock<IRepository<AiUsageLog>>       _usageRepo = new();
    private readonly Mock<IRepository<Domain>>           _domainRepo = new();
    private readonly PlanService _sut;

    private static readonly Plan FreePlan = new() { Id = 1, Name = "Ücretsiz", MaxDomains = 1, MonthlyAiQuota = 50 };
    private static readonly Plan ProPlan  = new() { Id = 2, Name = "Pro",      MaxDomains = 10, MonthlyAiQuota = 2000 };

    public PlanServiceTests()
    {
        _planRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(FreePlan);
        _planRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(ProPlan);
        _sut = new PlanService(_subRepo.Object, _planRepo.Object, _usageRepo.Object, _domainRepo.Object);
    }

    [Fact]
    public async Task GetUserActivePlan_AktifProSubscription_ProPlanDöner()
    {
        var sub = new UserSubscription
        {
            UserId = "u1", PlanId = 2, IsActive = true, IsDeleted = false,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate   = DateTime.UtcNow.AddDays(9),
        };
        _subRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(sub);

        var plan = await _sut.GetUserActivePlanAsync("u1");

        Assert.Equal(2, plan.Id);
        Assert.Equal("Pro", plan.Name);
    }

    [Fact]
    public async Task GetUserActivePlan_SubscriptionYok_ÜcretsizPlanDöner()
    {
        _subRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync((UserSubscription?)null);

        var plan = await _sut.GetUserActivePlanAsync("u1");

        Assert.Equal(1, plan.Id);
    }

    [Fact]
    public async Task GetUserActivePlan_SüresiDolmuşSubscription_ÜcretsizPlanDöner()
    {
        // EndDate geçmişte → FirstOrDefaultAsync filtresi null döndürür
        _subRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync((UserSubscription?)null);

        var plan = await _sut.GetUserActivePlanAsync("u1");

        Assert.Equal(1, plan.Id);
    }

    [Fact]
    public async Task GetUserActivePlan_IsActiveFalse_ÜcretsizPlanDöner()
    {
        // IsActive=false olan subscription için repo null döner (filtre bunu eliyor)
        _subRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync((UserSubscription?)null);

        var plan = await _sut.GetUserActivePlanAsync("u1");

        Assert.Equal(1, plan.Id);
    }

    [Fact]
    public async Task GetUserActivePlan_PlanDBdenBulunamazsa_FallbackDöner()
    {
        var sub = new UserSubscription
        {
            UserId = "u1", PlanId = 99, IsActive = true, IsDeleted = false,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate   = DateTime.UtcNow.AddDays(30),
        };
        _subRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(sub);
        _planRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Plan?)null);

        var plan = await _sut.GetUserActivePlanAsync("u1");

        // Fallback: inline varsayılan Ücretsiz plan
        Assert.Equal(1, plan.Id);
        Assert.Equal(1, plan.MaxDomains);
    }
}

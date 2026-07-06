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

    private static readonly Plan FreePlan       = new() { Id = 1, Name = "Ücretsiz", MaxDomains = 1,   MonthlyAiQuota = 0,      MonthlyPrice = 0 };
    private static readonly Plan ProPlan        = new() { Id = 2, Name = "Pro",      MaxDomains = 3,   MonthlyAiQuota = 500,    MonthlyPrice = 600 };
    private static readonly Plan EnterprisePlan = new() { Id = 3, Name = "Kurumsal", MaxDomains = 999, MonthlyAiQuota = 999999, MonthlyPrice = 0 };

    public PlanServiceTests()
    {
        // PlanService planları FindAsync ile çeker; mock, gerçek predicate'i
        // bellek içi plan listesine uygular.
        _planRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Plan, bool>>>()))
                 .ReturnsAsync((Expression<Func<Plan, bool>> predicate) =>
                     new[] { FreePlan, ProPlan, EnterprisePlan }.Where(predicate.Compile()).ToList());
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
        _subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new[] { sub });

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
    public async Task GetUserActivePlan_KurumsalVePro_KurumsalDöner()
    {
        // Kurumsal'ın fiyatı 0 (teklif usulü) — fiyata göre sıralansaydı Pro kazanırdı.
        // TierRank sayesinde Kurumsal en üst kademe olarak seçilmelidir.
        var now = DateTime.UtcNow;
        var subs = new[]
        {
            new UserSubscription { UserId = "u1", PlanId = 2, IsActive = true, IsDeleted = false, StartDate = now.AddDays(-5), EndDate = now.AddDays(30) },
            new UserSubscription { UserId = "u1", PlanId = 3, IsActive = true, IsDeleted = false, StartDate = now.AddDays(-5), EndDate = now.AddDays(30) },
        };
        _subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(subs);

        var plan = await _sut.GetUserActivePlanAsync("u1");

        Assert.Equal(3, plan.Id);
        Assert.Equal("Kurumsal", plan.Name);
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
        _subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new[] { sub });

        var plan = await _sut.GetUserActivePlanAsync("u1");

        // Fallback: inline varsayılan Ücretsiz plan
        Assert.Equal(1, plan.Id);
        Assert.Equal(1, plan.MaxDomains);
    }
}

using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class WidgetAnalyticsService : IWidgetAnalyticsService
{
    private readonly IRepository<WidgetUsageLog> _logs;

    public WidgetAnalyticsService(IRepository<WidgetUsageLog> logs)
        => _logs = logs;

    public async Task<IReadOnlyList<DailyUsageCount>> GetDailyOpenCountsAsync(int domainId, int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days).Date;
        var logs = await _logs.FindAsync(l =>
            l.DomainId == domainId &&
            l.EventType == WidgetEventTypes.WidgetOpened &&
            l.OccurredAt >= since);

        return logs
            .GroupBy(l => DateOnly.FromDateTime(l.OccurredAt))
            .Select(g => new DailyUsageCount(g.Key, g.Count()))
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<TopFeature>> GetTopFeaturesAsync(int domainId, int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days).Date;
        var logs = await _logs.FindAsync(l =>
            l.DomainId == domainId &&
            l.EventType == WidgetEventTypes.FeatureToggled &&
            l.FeatureName != null &&
            l.OccurredAt >= since);

        return logs
            .GroupBy(l => l.FeatureName!)
            .Select(g => new TopFeature(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .Take(10)
            .ToList();
    }

    public async Task<MonthlyStats> GetMonthlyStatsAsync(int domainId)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var logs = await _logs.FindAsync(l =>
            l.DomainId == domainId &&
            l.EventType == WidgetEventTypes.WidgetOpened &&
            l.OccurredAt >= lastMonthStart);

        var thisMonth = logs.Count(l => l.OccurredAt >= thisMonthStart);
        var lastMonth = logs.Count(l => l.OccurredAt < thisMonthStart);

        double change = lastMonth == 0
            ? (thisMonth > 0 ? 100 : 0)
            : Math.Round((thisMonth - lastMonth) / (double)lastMonth * 100, 1);

        return new MonthlyStats(thisMonth, lastMonth, change);
    }
}

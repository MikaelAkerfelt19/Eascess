namespace Eascess_Application.Services;

public record DailyUsageCount(DateOnly Date, int Count);
public record TopFeature(string FeatureName, int Count);
public record MonthlyStats(int ThisMonth, int LastMonth, double ChangePercent);

public interface IWidgetAnalyticsService
{
    /// <summary>Son N günün günlük widget açılma sayıları.</summary>
    Task<IReadOnlyList<DailyUsageCount>> GetDailyOpenCountsAsync(int domainId, int days = 30);

    /// <summary>En çok kullanılan erişilebilirlik özellikleri.</summary>
    Task<IReadOnlyList<TopFeature>> GetTopFeaturesAsync(int domainId, int days = 30);

    /// <summary>Bu ay vs geçen ay widget açılma karşılaştırması.</summary>
    Task<MonthlyStats> GetMonthlyStatsAsync(int domainId);
}

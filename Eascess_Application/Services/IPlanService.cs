using Eascess_Domain.Entities;

namespace Eascess_Application.Services;

public interface IPlanService
{
    /// <summary>
    /// Kullanıcının aktif plan bilgisini döndürür.
    /// Aktif abonelik yoksa varsayılan Ücretsiz plan döner.
    /// </summary>
    Task<Plan> GetUserActivePlanAsync(string userId);

    /// <summary>
    /// Kullanıcının bu ay kaç AI taraması kullandığını döndürür.
    /// </summary>
    Task<int> GetMonthlyAiUsageAsync(string userId);
}

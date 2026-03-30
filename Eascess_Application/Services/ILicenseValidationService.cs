namespace Eascess_Application.Services;

public interface ILicenseValidationService
{
    /// <summary>
    /// Verilen lisans anahtarının domain ile eşleşip eşleşmediğini doğrular.
    /// </summary>
    Task<LicenseValidationResult> ValidateAsync(Guid licenseKey, string domain);

    /// <summary>
    /// Sadece lisans anahtarının geçerliliğini (domain kontrolü olmadan) kontrol eder.
    /// Dinamik CORS middleware'i için kullanılır.
    /// </summary>
    Task<bool> DomainIsRegisteredAsync(string domainUrl);
}

public record LicenseValidationResult(bool Valid, string? Reason = null, string Plan = "free", DateTime? ExpiresAt = null);

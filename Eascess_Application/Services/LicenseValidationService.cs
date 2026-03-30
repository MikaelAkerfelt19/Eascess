using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class LicenseValidationService : ILicenseValidationService
{
    private readonly IRepository<Domain> _domainRepo;

    public LicenseValidationService(IRepository<Domain> domainRepo)
    {
        _domainRepo = domainRepo;
    }

    public async Task<LicenseValidationResult> ValidateAsync(Guid licenseKey, string domain)
    {
        if (licenseKey == Guid.Empty || string.IsNullOrWhiteSpace(domain))
            return new LicenseValidationResult(false, "invalid");

        var normalizedDomain = NormalizeDomain(domain);

        var match = await _domainRepo.FirstOrDefaultAsync(d =>
            d.LicenseKey == licenseKey &&
            d.IsDeleted != true);

        if (match is null)
            return new LicenseValidationResult(false, "invalid");

        if (!string.Equals(NormalizeDomain(match.DomainUrl), normalizedDomain, StringComparison.OrdinalIgnoreCase))
            return new LicenseValidationResult(false, "invalid");

        return new LicenseValidationResult(true, null, "free", null);
    }

    public async Task<bool> DomainIsRegisteredAsync(string domainUrl)
    {
        if (string.IsNullOrWhiteSpace(domainUrl)) return false;

        var normalized = NormalizeDomain(domainUrl);

        var match = await _domainRepo.FirstOrDefaultAsync(d =>
            d.DomainUrl == normalized &&
            d.IsDeleted != true);

        return match is not null;
    }

    private static string NormalizeDomain(string input)
    {
        // "https://example.com/path" → "example.com"
        // "example.com" → "example.com"
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return uri.Host.ToLowerInvariant();

        return input.ToLowerInvariant().TrimStart('/').Split('/')[0];
    }
}

using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class LicenseValidationService : ILicenseValidationService
{
    private readonly IRepository<Domain> _domainRepo;
    private readonly IPlanService _planService;

    public LicenseValidationService(IRepository<Domain> domainRepo, IPlanService planService)
    {
        _domainRepo = domainRepo;
        _planService = planService;
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

        var plan = await _planService.GetUserActivePlanAsync(match.UserId);

        // Plan kotası uygulaması: kullanıcı daha üst bir planda iken (örn. deneme/Pro)
        // birden çok domain eklemiş, ardından düşük plana düşmüş olabilir. Bu durumda
        // yalnızca planın izin verdiği kadar (en eski eklenen) domain çalışmaya devam eder;
        // limiti aşan domainler "plan_expired" alır.
        var userDomains = await _domainRepo.FindAsync(
            d => d.UserId == match.UserId && d.IsDeleted != true);

        var rank = (userDomains ?? Enumerable.Empty<Domain>())
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .ToList()
            .FindIndex(d => d.Id == match.Id);

        if (rank >= plan.MaxDomains)
            return new LicenseValidationResult(false, "plan_expired", plan.Name.ToLowerInvariant());

        return new LicenseValidationResult(true, null, plan.Name.ToLowerInvariant(), null);
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

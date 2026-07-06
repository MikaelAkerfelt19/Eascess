using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eascess.Controllers.Api;

[ApiController]
[Route("api/widget")]
[EnableRateLimiting("public-api")]
public class WidgetApiController : ControllerBase
{
    private readonly IWidgetService _widgetService;
    private readonly IRepository<Domain> _domainRepo;
    private readonly IUnitOfWork _unitOfWork;

    public WidgetApiController(
        IWidgetService widgetService,
        IRepository<Domain> domainRepo,
        IUnitOfWork unitOfWork)
    {
        _widgetService = widgetService;
        _domainRepo = domainRepo;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Müşteri sitesindeki JS widget bu endpoint'i çağırır.
    /// Lisans geçerliyse widget ayarlarını JSON olarak döner
    /// ve domain'i otomatik olarak doğrulanmış (IsVerified) işaretler.
    /// </summary>
    // GET /api/widget/config?key={licenseKey}
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig([FromQuery] Guid key)
    {
        if (key == Guid.Empty)
            return BadRequest(new { error = "Geçersiz lisans anahtarı." });

        var config = await _widgetService.GetConfigByLicenseKeyAsync(key);

        if (config is null)
            return NotFound(new { error = "Lisans anahtarı bulunamadı veya aktif değil." });

        // İlk başarılı çağrıda domain'i otomatik doğrula
        // Güvenlik: sadece isteğin gerçekten o domain'den geldiği doğrulanırsa işaretlenir
        var origin = Request.Headers.Origin.FirstOrDefault()
                  ?? Request.Headers.Referer.FirstOrDefault();
        await AutoVerifyDomainAsync(key, origin);

        return Ok(config);
    }

    private async Task AutoVerifyDomainAsync(Guid licenseKey, string? requestOrigin)
    {
        var domain = await _domainRepo.FirstOrDefaultAsync(
            d => d.LicenseKey == licenseKey && d.IsDeleted != true && d.IsVerified != true);

        if (domain is null) return;

        // Origin header yoksa veya domain URL'iyle eşleşmiyorsa doğrulama yapma
        if (string.IsNullOrWhiteSpace(requestOrigin))
            return;

        if (!OriginMatchesDomain(requestOrigin, domain.DomainUrl))
            return;

        domain.IsVerified = true;
        _domainRepo.Update(domain);
        await _unitOfWork.SaveChangesAsync();
    }

    private static bool OriginMatchesDomain(string origin, string domainUrl)
    {
        try
        {
            var originHost = new Uri(origin).Host.TrimStart('.');
            var domainHost = domainUrl
                .Replace("https://", "").Replace("http://", "")
                .Split('/')[0].Split(':')[0].TrimStart('.');
            return string.Equals(originHost, domainHost, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers.Api;

[ApiController]
[Route("api/widget")]
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
        await AutoVerifyDomainAsync(key);

        return Ok(config);
    }

    private async Task AutoVerifyDomainAsync(Guid licenseKey)
    {
        var domain = await _domainRepo.FirstOrDefaultAsync(
            d => d.LicenseKey == licenseKey && d.IsDeleted != true && d.IsVerified != true);

        if (domain is null) return;

        domain.IsVerified = true;
        _domainRepo.Update(domain);
        await _unitOfWork.SaveChangesAsync();
    }
}

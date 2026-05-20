using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers.Api;

public record WidgetLogRequest(Guid LicenseKey, string Event, string? Feature);

[ApiController]
[Route("api/widget")]
public class WidgetLogApiController : ControllerBase
{
    private static readonly HashSet<string> AllowedEvents =
        new(StringComparer.OrdinalIgnoreCase)
        { "widget_opened", "feature_toggled", "ai_scan_used" };

    private readonly IRepository<Domain> _domainRepo;
    private readonly IRepository<WidgetUsageLog> _logRepo;
    private readonly IUnitOfWork _uow;

    public WidgetLogApiController(
        IRepository<Domain> domainRepo,
        IRepository<WidgetUsageLog> logRepo,
        IUnitOfWork uow)
    {
        _domainRepo = domainRepo;
        _logRepo    = logRepo;
        _uow        = uow;
    }

    // POST /api/widget/log
    // Fire-and-forget — her zaman 200 döner, widget'ı bloke etmez
    [HttpPost("log")]
    public async Task<IActionResult> Log([FromBody] WidgetLogRequest? request)
    {
        if (request is null || request.LicenseKey == Guid.Empty)
            return Ok(new { ok = true });

        if (!AllowedEvents.Contains(request.Event))
            return Ok(new { ok = true });

        var domain = await _domainRepo.FirstOrDefaultAsync(
            d => d.LicenseKey == request.LicenseKey && d.IsDeleted != true);

        if (domain is null)
            return Ok(new { ok = true });

        await _logRepo.AddAsync(new WidgetUsageLog
        {
            Id          = Guid.NewGuid(),
            DomainId    = domain.Id,
            EventType   = request.Event.ToLowerInvariant(),
            FeatureName = request.Feature,
            OccurredAt  = DateTime.UtcNow,
        });
        await _uow.SaveChangesAsync();

        return Ok(new { ok = true });
    }
}

using Eascess_Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eascess.Controllers.Api;

[ApiController]
[Route("api/license")]
[EnableRateLimiting("public-api")]
public class LicenseApiController : ControllerBase
{
    private readonly ILicenseValidationService _licenseService;

    public LicenseApiController(ILicenseValidationService licenseService)
    {
        _licenseService = licenseService;
    }

    // GET /api/license/validate?key={licenseKey}&domain={domain}
    [HttpGet("validate")]
    public async Task<IActionResult> Validate([FromQuery] Guid key, [FromQuery] string? domain)
    {
        if (key == Guid.Empty || string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { valid = false, reason = "invalid" });

        var result = await _licenseService.ValidateAsync(key, domain);

        if (!result.Valid)
            return Ok(new { valid = false, reason = result.Reason ?? "invalid" });

        return Ok(new
        {
            valid = true,
            plan = result.Plan,
            expiresAt = result.ExpiresAt
        });
    }
}

using Eascess_Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eascess.Controllers.Api;

[ApiController]
[Route("api/scan")]
public class AltTextApiController : ControllerBase
{
    private readonly IAiScanService _aiScanService;

    public AltTextApiController(IAiScanService aiScanService)
    {
        _aiScanService = aiScanService;
    }

    [HttpPost("alt-text")]
    public async Task<IActionResult> GenerateAltText(
        [FromBody] AltTextRequest request, CancellationToken ct)
    {
        if (request.LicenseKey == Guid.Empty)
            return BadRequest(new { error = true, code = "INVALID_REQUEST", message = "License key gerekli." });

        if (request.Images is null || request.Images.Count == 0)
            return BadRequest(new { error = true, code = "INVALID_REQUEST", message = "En az bir görsel URL'si gerekli." });

        if (request.Images.Count > 20)
            return BadRequest(new { error = true, code = "INVALID_REQUEST", message = "Tek istekte en fazla 20 görsel gönderilebilir." });

        try
        {
            var result = await _aiScanService.ProcessImagesAsync(request.LicenseKey, request.Images, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVALID_LICENSE")
        {
            return Unauthorized(new { error = true, code = "INVALID_LICENSE", message = "Geçersiz lisans anahtarı." });
        }
        catch (InvalidOperationException ex) when (ex.Message == "AI_DISABLED")
        {
            return BadRequest(new { error = true, code = "AI_DISABLED", message = "Bu domain için AI tarama devre dışı." });
        }
        catch (InvalidOperationException ex) when (ex.Message == "QUOTA_EXCEEDED")
        {
            return StatusCode(429, new { error = true, code = "QUOTA_EXCEEDED", message = "Aylık AI tarama kotanız doldu." });
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = true, code = "INTERNAL_ERROR", message = "Beklenmeyen bir hata oluştu." });
        }
    }
}

public class AltTextRequest
{
    public Guid LicenseKey { get; set; }
    public List<string> Images { get; set; } = new();
}

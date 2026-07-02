using Eascess_Application.Services;

namespace Eascess.Middleware;

/// <summary>
/// Yalnızca DB'de kayıtlı domainlere CORS izni verir.
/// AllowAnyOrigin(*) kullanmaz — her müşterinin domain'i ayrı ayrı doğrulanır.
/// </summary>
public class DynamicCorsMiddleware
{
    private readonly RequestDelegate _next;

    public DynamicCorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILicenseValidationService licenseService)
    {
        // Sadece API endpoint'leri için çalış
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var origin = context.Request.Headers.Origin.FirstOrDefault();

        if (string.IsNullOrEmpty(origin))
        {
            // Origin header'ı yok (Postman/curl/sunucu-sunucu): CORS header'ı eklemeden geç
            await _next(context);
            return;
        }

        // Yanıt origin'e göre değiştiği için Vary her durumda eklenmeli —
        // aksi halde ara cache'ler izinli origin'in yanıtını izinsize servis edebilir.
        context.Response.Headers.Append("Vary", "Origin");

        var isRegistered = await licenseService.DomainIsRegisteredAsync(origin);

        if (isRegistered)
        {
            // Widget /api/widget/log ve /api/scan/alt-text uçlarına JSON POST atar;
            // preflight'ın geçmesi için POST da izinli olmalıdır.
            context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
            context.Response.Headers.Append("Access-Control-Max-Age", "3600");
        }

        // OPTIONS preflight isteğini bitir
        if (context.Request.Method == "OPTIONS")
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await _next(context);
    }
}

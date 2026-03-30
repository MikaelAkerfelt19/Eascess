using Eascess_Application.Services;

namespace Eascess.Middleware;

/// <summary>
/// Yalnızca DB'de kayıtlı domainlere CORS izni verir.
/// AllowAnyOrigin(*) kullanmaz — her müşterinin domain'i ayrı ayrı doğrulanır.
/// </summary>
public class DynamicCorsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public DynamicCorsMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
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
            // Origin header'ı yok: Postman/curl isteği
            // Development'ta geç, Production'da CORS header'ı ekleme
            if (!_env.IsDevelopment())
            {
                await _next(context);
                return;
            }

            await _next(context);
            return;
        }

        var isRegistered = await licenseService.DomainIsRegisteredAsync(origin);

        if (isRegistered)
        {
            context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
            context.Response.Headers.Append("Vary", "Origin");
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

namespace Eascess.Middleware;

/// <summary>
/// /api rotalarında yakalanmamış hataları loglayıp standart JSON hata gövdesiyle
/// 500 döner. MVC sayfaları etkilenmez — onlar UseExceptionHandler("/Home/Error")
/// üzerinden HTML hata sayfası almaya devam eder.
/// </summary>
public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API isteği sırasında yakalanmamış hata: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                error = true,
                code = "INTERNAL_ERROR",
                message = "Beklenmeyen bir hata oluştu.",
            });
        }
    }
}

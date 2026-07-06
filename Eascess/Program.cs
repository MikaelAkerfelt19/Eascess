using System.Threading.RateLimiting;
using Eascess.Middleware;
using Eascess_Application.Options;
using Eascess_Application.Security;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Eascess_Infrastructure.Persistence;
using Eascess_Infrastructure.Repositories;
using Eascess_Infrastructure.Scanning;
using Eascess_Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EaccessDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
    // Brute-force koruması: 5 hatalı denemede 5 dakika kilit
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<EaccessDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

// Repository & UnitOfWork
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Application services
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<IScanReportService, ScanReportService>();
builder.Services.AddScoped<IWidgetService, WidgetService>();
builder.Services.AddScoped<IWidgetSettingService, WidgetSettingService>();
builder.Services.AddScoped<IScanService, WcagScanService>();
builder.Services.AddScoped<ILicenseValidationService, LicenseValidationService>();

// HttpClient — WCAG tarayıcı için.
// SSRF koruması: ConnectCallback her TCP bağlantısında (yönlendirmeler dahil)
// hedef IP'yi doğrular; özel/rezerve ağlara bağlanmayı engeller.
builder.Services.AddHttpClient("WcagScanner", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Eascess-Scanner/1.0 (WCAG accessibility audit; +https://eascess.io)");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        ConnectCallback = PrivateNetworkGuard.SafeConnectAsync,
    };
    // SSL doğrulamayı yalnızca geliştirme ortamında devre dışı bırak
    if (builder.Environment.IsDevelopment())
        handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
    return handler;
});

// AI görsel indirme istemcisi — kullanıcı tarafından verilen görsel URL'leri
// buradan indirilir; SSRF için en kritik yüzey. Yönlendirme kapalı (görsel
// indirmede gerekmez) + ConnectCallback ile IP doğrulaması.
builder.Services.AddHttpClient("AltTextImageDownloader", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Eascess-AltText/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    ConnectCallback = PrivateNetworkGuard.SafeConnectAsync,
});

// AI Alt-Text — Gemini
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

builder.Services.AddHttpClient<GeminiAltTextGeneratorService>(client =>
{
    var timeout = builder.Configuration.GetValue("AltTextGenerator:Gemini:TimeoutSeconds", 15);
    client.Timeout = TimeSpan.FromSeconds(timeout);
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(2, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

builder.Services.AddScoped<IAltTextGeneratorService>(sp =>
    sp.GetRequiredService<GeminiAltTextGeneratorService>());
builder.Services.AddScoped<IAiScanService, AiScanService>();
builder.Services.AddScoped<IWidgetAnalyticsService, WidgetAnalyticsService>();

// Email & Monthly Report (ADIM 7)
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IMonthlyReportService, MonthlyReportService>();
builder.Services.AddHostedService<MonthlyReportJob>();

// Public scan & trial reminder (ADIM 9)
builder.Services.AddScoped<IPublicScanService, PublicScanService>();
builder.Services.AddHostedService<TrialReminderJob>();

// Plan düşüşü temizliği: süresi dolan ücretli abonelikler, domain silme,
// 60 günlük logo bekleme (TrialExpiryJob'ın genişletilmiş hali)
builder.Services.AddScoped<IDowngradeCleanupService, DowngradeCleanupService>();
builder.Services.AddHostedService<DowngradeCleanupJob>();

// Pro ve üzeri planlar için otomatik yeniden tarama (fiyatlandırma vaadi)
builder.Services.AddHostedService<AutoRescanJob>();
builder.Services.AddMemoryCache();

// Rate limiting — public API endpoint'leri IP bazlı sınırlandırılır.
// Not: AddFixedWindowLimiter tüm istemcilerin paylaştığı TEK limit oluşturur;
// IP başına limit için partitioned policy gerekir.
builder.Services.AddRateLimiter(options =>
{
    // AI endpoint: IP başına dakikada 10 istek
    options.AddPolicy("ai-scan", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Lisans doğrulama / widget config / widget log: IP başına dakikada 60 istek
    options.AddPolicy("public-api", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.RejectionStatusCode = 429;
});

// CORS — Statik AllowAnyOrigin(*) yerine DynamicCorsMiddleware kullanılıyor.
// Her müşterinin domain'i DB'den doğrulanarak izin veriliyor.

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// /api rotaları için JSON hata gövdesi (widget fetch'leri HTML hata sayfası parse edemez)
app.UseMiddleware<ApiExceptionMiddleware>();

// Güvenlik başlıkları
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    ctx.Response.Headers["X-Frame-Options"]           = "SAMEORIGIN";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
    // widget.js müşteri sitelerine embed edildiği için 'unsafe-inline' gereklidir
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self'; " +
        "frame-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self';";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<DynamicCorsMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Integration testlerin WebApplicationFactory<Program> kullanabilmesi için gerekli
public partial class Program { }

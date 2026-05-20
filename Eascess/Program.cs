using Eascess.Middleware;
using Eascess_Application.Options;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Eascess_Infrastructure.Persistence;
using Eascess_Infrastructure.Repositories;
using Eascess_Infrastructure.Scanning;
using Eascess_Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
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

// HttpClient — WCAG tarayıcı için
builder.Services.AddHttpClient("WcagScanner", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Eascess-Scanner/1.0 (WCAG accessibility audit; +https://eascess.io)");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5,
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
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
builder.Services.AddMemoryCache();

// CORS — Statik AllowAnyOrigin(*) yerine DynamicCorsMiddleware kullanılıyor.
// Her müşterinin domain'i DB'den doğrulanarak izin veriliyor.

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<DynamicCorsMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Integration testlerin WebApplicationFactory<Program> kullanabilmesi için gerekli
public partial class Program { }

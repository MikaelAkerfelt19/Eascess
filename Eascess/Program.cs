using Eascess.Middleware;
using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Eascess_Infrastructure.Persistence;
using Eascess_Infrastructure.Repositories;
using Eascess_Infrastructure.Scanning;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

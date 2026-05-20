using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Eascess.Tests.Integration;

/// <summary>
/// POST /api/scan/alt-text endpoint integration testleri.
/// Gerçek HTTP pipeline + InMemory DB, Gemini yerine mock generator.
/// </summary>
public class AltTextApiIntegrationTests : IClassFixture<AltTextApiTestFactory>
{
    private readonly HttpClient _client;
    private readonly AltTextApiTestFactory _factory;

    private static readonly Guid ValidKey = AltTextApiTestFactory.SeedLicenseKey;

    public AltTextApiIntegrationTests(AltTextApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        factory.SeedDatabase(db =>
        {
            if (db.Domains.Any()) return; // zaten seed edilmiş

            db.Domains.Add(new Domain
            {
                UserId = "test-user",
                LicenseKey = SeedLicenseKey,
                DomainUrl = "ai-test.local",
                IsDeleted = false,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow
            });

            db.WidgetSettings.Add(new WidgetSetting
            {
                DomainId = 1,
                IsAiEnabled = true,
                IsActive = true,
                ThemeColor = "#000",
                Position = "bottom-right",
                Language = "tr"
            });
        });
    }

    private static Guid SeedLicenseKey => AltTextApiTestFactory.SeedLicenseKey;

    [Fact]
    public async Task AltText_GeçerliKeyVeGörseller_Returns200WithResults()
    {
        var response = await _client.PostAsJsonAsync("/api/scan/alt-text", new
        {
            licenseKey = ValidKey,
            images = new[] { "https://example.com/cat.jpg", "https://example.com/dog.jpg" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await ParseJsonAsync(response);
        var results = json.GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());

        var first = results[0];
        Assert.True(first.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("altText").GetString()));
    }

    [Fact]
    public async Task AltText_GeçersizLisans_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/scan/alt-text", new
        {
            licenseKey = Guid.NewGuid(),
            images = new[] { "https://example.com/img.jpg" }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await ParseJsonAsync(response);
        Assert.Equal("INVALID_LICENSE", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AltText_EmptyGuid_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/scan/alt-text", new
        {
            licenseKey = Guid.Empty,
            images = new[] { "https://example.com/img.jpg" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AltText_BoşGörselListesi_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/scan/alt-text", new
        {
            licenseKey = ValidKey,
            images = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AltText_21Görsel_Returns400()
    {
        var images = Enumerable.Range(1, 21).Select(i => $"https://example.com/img{i}.jpg").ToList();

        var response = await _client.PostAsJsonAsync("/api/scan/alt-text", new
        {
            licenseKey = ValidKey,
            images
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AltText_QuotaRemaining_Döner()
    {
        var response = await _client.PostAsJsonAsync("/api/scan/alt-text", new
        {
            licenseKey = ValidKey,
            images = new[] { "https://example.com/quota-test.jpg" }
        });

        var json = await ParseJsonAsync(response);
        Assert.True(json.TryGetProperty("quotaRemaining", out var quota));
        Assert.True(quota.GetInt32() >= 0);
    }

    private static async Task<JsonElement> ParseJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement;
    }
}

/// <summary>
/// AltText testleri için özel factory.
/// IAltTextGeneratorService'i mock ile değiştirir, gerçek Gemini çağrısı yapmaz.
/// </summary>
public class AltTextApiTestFactory : EaccessWebAppFactory
{
    public static readonly Guid SeedLicenseKey = Guid.NewGuid();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Gemini'yi mock ile değiştir
            services.RemoveAll<IAltTextGeneratorService>();
            services.AddScoped<IAltTextGeneratorService, FakeAltTextGeneratorService>();
        });
    }
}

/// <summary>
/// Gemini API yerine kullanılan fake generator.
/// Her zaman başarılı sonuç döner.
/// </summary>
public class FakeAltTextGeneratorService : IAltTextGeneratorService
{
    public Task<AltTextResult> GenerateAsync(string imageUrl, CancellationToken ct = default)
    {
        var altText = $"Test alt text for {imageUrl.Split('/').Last()}";
        return Task.FromResult(new AltTextResult(true, altText, null, 50));
    }
}

using Eascess_Domain.Entities;
using System.Net;
using System.Text.Json;

namespace Eascess.Tests.Integration;

/// <summary>
/// ADIM 3C — Uçtan uca test senaryosu (otomatik).
/// Roadmap'teki 6 senaryo bu testlerle karşılanır.
/// </summary>
public class LicenseApiIntegrationTests : IClassFixture<EaccessWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly EaccessWebAppFactory _factory;

    private static readonly Guid ValidKey = Guid.NewGuid();
    private static readonly Guid DeletedKey = Guid.NewGuid();
    private const string ValidDomain = "test.local";

    public LicenseApiIntegrationTests(EaccessWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        factory.SeedDatabase(db =>
        {
            // Senaryo 1-5: Aktif domain
            db.Domains.Add(new Domain
            {
                UserId = "user-seed",
                LicenseKey = ValidKey,
                DomainUrl = ValidDomain,
                IsDeleted = false,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow
            });

            // Senaryo 6: Silinmiş domain
            db.Domains.Add(new Domain
            {
                UserId = "user-seed",
                LicenseKey = DeletedKey,
                DomainUrl = "deleted.local",
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
        });
    }

    // Senaryo 3: geçerli key + doğru domain → { valid: true }
    [Fact]
    public async Task Validate_GeçerliKeyVeDoğruDomain_Returns200Valid()
    {
        var response = await _client.GetAsync($"/api/license/validate?key={ValidKey}&domain={ValidDomain}");
        var json = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.GetProperty("valid").GetBoolean());
    }

    // Senaryo 4: geçerli key + yanlış domain → { valid: false }
    [Fact]
    public async Task Validate_GeçerliKey_YanlışDomain_Returns200Invalid()
    {
        var response = await _client.GetAsync($"/api/license/validate?key={ValidKey}&domain=hacker.com");
        var json = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(json.GetProperty("valid").GetBoolean());
        Assert.Equal("invalid", json.GetProperty("reason").GetString());
    }

    // Senaryo 6: Silinmiş domain → { valid: false }
    [Fact]
    public async Task Validate_SilinmisDomain_Returns200Invalid()
    {
        var response = await _client.GetAsync($"/api/license/validate?key={DeletedKey}&domain=deleted.local");
        var json = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(json.GetProperty("valid").GetBoolean());
    }

    // Güvenlik: bilinmeyen key leak etmemeli
    [Fact]
    public async Task Validate_BilinmeyenKey_ReasonDetaylıOlmamali()
    {
        var response = await _client.GetAsync($"/api/license/validate?key={Guid.NewGuid()}&domain=example.com");
        var json = await ParseJsonAsync(response);

        Assert.False(json.GetProperty("valid").GetBoolean());
        // "invalid" dışında bir reason döndürülmemeli
        Assert.Equal("invalid", json.GetProperty("reason").GetString());
    }

    // Bad request: eksik parametre
    [Fact]
    public async Task Validate_EksikDomain_Returns400()
    {
        var response = await _client.GetAsync($"/api/license/validate?key={ValidKey}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Validate_EmptyGuid_Returns400()
    {
        var response = await _client.GetAsync($"/api/license/validate?key={Guid.Empty}&domain=example.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // CORS: kayıtlı domain'den gelen istek CORS header almalı
    [Fact]
    public async Task DynamicCors_KayıtlıOrigin_CorsHeaderDönmeli()
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/license/validate?key={ValidKey}&domain={ValidDomain}");
        request.Headers.Add("Origin", $"https://{ValidDomain}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Kayıtlı origin için CORS header eklenmeli.");
        Assert.Equal($"https://{ValidDomain}",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    // CORS: kayıtsız domain için CORS header gelmemeli
    [Fact]
    public async Task DynamicCors_KayıtsızOrigin_CorsHeaderOlmamali()
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/license/validate?key={ValidKey}&domain={ValidDomain}");
        request.Headers.Add("Origin", "https://unauthorized.com");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Kayıtsız origin için CORS header eklenmemeli.");
    }

    // ─── Widget Config API ────────────────────────────────────────────────────

    // Senaryo 5: /api/widget/config?key={key} → config JSON dönmeli
    [Fact]
    public async Task WidgetConfig_GeçerliKey_Returns200Config()
    {
        var response = await _client.GetAsync($"/api/widget/config?key={ValidKey}");
        var json = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.TryGetProperty("position", out _), "position alanı olmalı");
        Assert.True(json.TryGetProperty("themeColor", out _), "themeColor alanı olmalı");
        Assert.True(json.TryGetProperty("language", out _), "language alanı olmalı");
    }

    [Fact]
    public async Task WidgetConfig_GeçersizKey_Returns404()
    {
        var response = await _client.GetAsync($"/api/widget/config?key={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ParseJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement;
    }
}

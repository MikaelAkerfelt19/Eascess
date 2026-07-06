using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eascess_Application.Options;
using Eascess_Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eascess_Infrastructure.Services;

public class GeminiAltTextGeneratorService : IAltTextGeneratorService
{
    private readonly HttpClient _httpClient;           // Gemini API çağrıları (Polly retry ile)
    private readonly IHttpClientFactory _httpClientFactory; // Görsel indirme (retry yok)
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAltTextGeneratorService> _logger;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    public GeminiAltTextGeneratorService(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiOptions> options,
        ILogger<GeminiAltTextGeneratorService> logger)
    {
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AltTextResult> GenerateAsync(string imageUrl, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // 1. Görseli indir
            var (imageBytes, mimeType) = await DownloadImageAsync(imageUrl, ct);

            if (imageBytes is null)
                return new AltTextResult(false, null, "download_failed", (int)sw.ElapsedMilliseconds);

            // 2. Base64 encode
            var base64 = Convert.ToBase64String(imageBytes);

            // 3. Gemini API çağrısı
            var altText = await CallGeminiAsync(base64, mimeType!, ct);

            if (string.IsNullOrWhiteSpace(altText))
                return new AltTextResult(false, null, "empty_response", (int)sw.ElapsedMilliseconds);

            // 4. Trim + uzunluk limiti
            altText = altText.Trim().Trim('"');
            if (altText.Length > _options.MaxAltTextLength)
                altText = altText[.._options.MaxAltTextLength].TrimEnd() + "…";

            return new AltTextResult(true, altText, null, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new AltTextResult(false, null, "timeout", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini alt text generation failed for {Url}", imageUrl);
            return new AltTextResult(false, null, ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }

    private async Task<(byte[]? Bytes, string? MimeType)> DownloadImageAsync(
        string imageUrl, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5)); // görsel indirme için 5 sn limit

        using var downloadClient = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.UserAgent.ParseAdd("Eascess-AltText/1.0");

        using var response = await downloadClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Image download failed: {Status} for {Url}", response.StatusCode, imageUrl);
            return (null, null);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        if (!AllowedMimeTypes.Contains(contentType))
        {
            _logger.LogWarning("Unsupported image type: {Type} for {Url}", contentType, imageUrl);
            return (null, null);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);

        if (bytes.Length > _options.MaxImageSizeBytes)
        {
            _logger.LogWarning("Image too large: {Size} bytes for {Url}", bytes.Length, imageUrl);
            return (null, null);
        }

        return (bytes, contentType);
    }

    private async Task<string?> CallGeminiAsync(string base64Image, string mimeType, CancellationToken ct)
    {
        // API anahtarı query string yerine header ile gönderilir —
        // URL'ler log/proxy kayıtlarına düştüğü için anahtar sızıntısına yol açabilir.
        var url = $"{_options.ApiEndpoint.TrimEnd('/')}/{_options.Model}:generateContent";

        var payload = new GeminiRequest
        {
            Contents = new[]
            {
                new GeminiContent
                {
                    Parts = new GeminiPart[]
                    {
                        new GeminiTextPart { Text = _options.PromptTemplate },
                        new GeminiInlineDataPart
                        {
                            InlineData = new InlineData
                            {
                                MimeType = mimeType,
                                Data = base64Image
                            }
                        }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Gemini API returned {Status}: {Body}", response.StatusCode, errorBody);
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<GeminiResponse>(ct);
        return json?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
    }

    #region Gemini API Models

    private class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
    }

    [JsonDerivedType(typeof(GeminiTextPart))]
    [JsonDerivedType(typeof(GeminiInlineDataPart))]
    private class GeminiPart { }

    private class GeminiTextPart : GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private class GeminiInlineDataPart : GeminiPart
    {
        [JsonPropertyName("inline_data")]
        public InlineData InlineData { get; set; } = new();
    }

    private class InlineData
    {
        [JsonPropertyName("mime_type")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiResponseContent? Content { get; set; }
    }

    private class GeminiResponseContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiResponsePart>? Parts { get; set; }
    }

    private class GeminiResponsePart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    #endregion
}

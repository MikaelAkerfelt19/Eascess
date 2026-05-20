namespace Eascess_Application.Options;

public class GeminiOptions
{
    public const string SectionName = "AltTextGenerator:Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public string ApiEndpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxRetries { get; set; } = 2;
    public long MaxImageSizeBytes { get; set; } = 5_242_880; // 5 MB
    public int MaxAltTextLength { get; set; } = 125;
    public string PromptTemplate { get; set; } =
        "Bu görsel için kısa ve açıklayıcı bir alt metin üret. Türkçe cevap ver. Maksimum 125 karakter. Sadece alt metni döndür, başka açıklama ekleme.";
}

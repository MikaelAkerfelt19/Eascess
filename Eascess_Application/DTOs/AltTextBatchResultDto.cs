namespace Eascess_Application.DTOs;

public class AltTextBatchResultDto
{
    public List<AltTextResultItemDto> Results { get; set; } = new();
    public int ProcessedCount { get; set; }
    public int CachedCount { get; set; }
    public int QuotaRemaining { get; set; }
}

public class AltTextResultItemDto
{
    public string Url { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool FromCache { get; set; }
}

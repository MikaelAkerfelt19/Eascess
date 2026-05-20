namespace Eascess_Application.Services;

public interface IAltTextGeneratorService
{
    Task<AltTextResult> GenerateAsync(string imageUrl, CancellationToken ct = default);
}

public record AltTextResult(
    bool Success,
    string? AltText,
    string? ErrorReason,
    int ResponseTimeMs);

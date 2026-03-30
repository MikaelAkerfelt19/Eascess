namespace Eascess_Infrastructure.Scanning;

/// <summary>Tek bir WCAG denetim kuralını tanımlar.</summary>
internal sealed record WcagIssue(
    string ErrorCode,
    string ErrorDescription,
    string WcagLevel,   // "A" | "AA" | "AAA"
    string Severity,    // "Critical" | "Warning" | "Info"
    string ElementSelector
);

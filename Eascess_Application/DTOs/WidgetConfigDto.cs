namespace Eascess_Application.DTOs;

public class WidgetConfigDto
{
    public string ThemeColor { get; set; } = "#0056b3";
    public string Position { get; set; } = "bottom-right";
    public string Language { get; set; } = "tr";
    public bool IsAiEnabled { get; set; } = true;
    public string DomainUrl { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? WidgetTitle { get; set; }
    public bool PoweredByVisible { get; set; } = true;
}

namespace Eascess_Application.DTOs;

public class WidgetSettingDto
{
    public int Id { get; set; }
    public int DomainId { get; set; }
    public string DomainUrl { get; set; } = "";
    public string ThemeColor { get; set; } = "#38bdf8";
    public string Position { get; set; } = "bottom-right";
    public string Language { get; set; } = "tr";
    public bool IsAiEnabled { get; set; } = true;
}

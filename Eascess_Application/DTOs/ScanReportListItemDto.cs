namespace Eascess_Application.DTOs;

public class ScanReportListItemDto
{
    public int Id { get; set; }
    public int DomainId { get; set; }
    public string DomainUrl { get; set; } = "";
    public string ScanType { get; set; } = "Manual";
    public int WcagScore { get; set; }
    public int ErrorCount { get; set; }
    public DateTime ScanDate { get; set; }
}

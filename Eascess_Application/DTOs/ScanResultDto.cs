namespace Eascess_Application.DTOs;

public class ScanResultDto
{
    public bool Success { get; set; }
    public int DomainId { get; set; }
    public int ReportId { get; set; }
    public int WcagScore { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
}

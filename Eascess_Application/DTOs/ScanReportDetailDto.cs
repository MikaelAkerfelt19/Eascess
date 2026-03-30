namespace Eascess_Application.DTOs;

public class ScanReportDetailDto
{
    public int Id { get; set; }
    public int DomainId { get; set; }
    public string DomainUrl { get; set; } = "";
    public string ScanType { get; set; } = "Manual";
    public int WcagScore { get; set; }
    public int ErrorCount { get; set; }
    public DateTime ScanDate { get; set; }
    public List<ScanIssueDto> Issues { get; set; } = new();
}

public class ScanIssueDto
{
    public int Id { get; set; }
    public string PageUrl { get; set; } = "";
    public string? ElementSelector { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDescription { get; set; }
    public string? WcagLevel { get; set; }
    public string? Severity { get; set; }
}

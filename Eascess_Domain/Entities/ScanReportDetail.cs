using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;    

[Index("PageId", Name = "IX_ScanReportDetails_PageId")]
[Index("ScanReportId", Name = "IX_ScanReportDetails_ScanReportId")]
public partial class ScanReportDetail
{
    [Key]
    public int Id { get; set; }

    public int ScanReportId { get; set; }

    public int PageId { get; set; }

    public string? ElementSelector { get; set; }

    [StringLength(100)]
    public string? ErrorCode { get; set; }

    public string? ErrorDescription { get; set; }

    [StringLength(10)]
    public string? WcagLevel { get; set; }

    [StringLength(50)]
    public string? Severity { get; set; }

    [ForeignKey("PageId")]
    [InverseProperty("ScanReportDetails")]
    public virtual Page Page { get; set; } = null!;

    [ForeignKey("ScanReportId")]
    [InverseProperty("ScanReportDetails")]
    public virtual ScanReport ScanReport { get; set; } = null!;
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("DomainId", "ScanDate", Name = "IX_ScanReports_DomainId_ScanDate", IsDescending = new[] { false, true })]
public partial class ScanReport
{
    [Key]
    public int Id { get; set; }

    public int DomainId { get; set; }

    [StringLength(50)]
    public string? ScanType { get; set; }

    public int WcagScore { get; set; }

    public int ErrorCount { get; set; }

    public DateTime ScanDate { get; set; }

    [ForeignKey("DomainId")]
    [InverseProperty("ScanReports")]
    public virtual Domain Domain { get; set; } = null!;

    [InverseProperty("ScanReport")]
    public virtual ICollection<ScanReportDetail> ScanReportDetails { get; set; } = new List<ScanReportDetail>();
}

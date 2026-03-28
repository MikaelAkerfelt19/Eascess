using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

public partial class Page
{
    [Key]
    public int Id { get; set; }

    public int DomainId { get; set; }

    public string PageUrl { get; set; } = null!;

    public DateTime? LastScannedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    [ForeignKey("DomainId")]
    [InverseProperty("Pages")]
    public virtual Domain Domain { get; set; } = null!;

    [InverseProperty("Page")]
    public virtual ICollection<ScanReportDetail> ScanReportDetails { get; set; } = new List<ScanReportDetail>();
}

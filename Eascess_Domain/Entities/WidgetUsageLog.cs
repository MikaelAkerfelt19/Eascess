using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("DomainId", "OccurredAt", Name = "IX_WidgetUsageLog_Domain_Date")]
public class WidgetUsageLog
{
    [Key]
    public Guid Id { get; set; }

    public int DomainId { get; set; }

    /// <summary>widget_opened | feature_toggled | ai_scan_used</summary>
    [StringLength(50)]
    public string EventType { get; set; } = null!;

    /// <summary>Toggle adı — yalnızca feature_toggled event'inde dolu</summary>
    [StringLength(100)]
    public string? FeatureName { get; set; }

    public DateTime OccurredAt { get; set; }

    [ForeignKey("DomainId")]
    [InverseProperty("WidgetUsageLogs")]
    public virtual Domain Domain { get; set; } = null!;
}

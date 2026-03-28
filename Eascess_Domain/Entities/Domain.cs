using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("UserId", Name = "IX_Domains_UserId")]
[Index("DomainUrl", Name = "UQ_Domains_DomainUrl", IsUnique = true)]
[Index("LicenseKey", Name = "UQ_Domains_LicenseKey", IsUnique = true)]
public partial class Domain
{
    [Key]
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    [StringLength(255)]
    public string DomainUrl { get; set; } = null!;

    public Guid LicenseKey { get; set; }

    public bool? IsVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    [InverseProperty("Domain")]
    public virtual ICollection<AiProcessingQueue> AiProcessingQueues { get; set; } = new List<AiProcessingQueue>();

    [InverseProperty("Domain")]
    public virtual ICollection<AiUsageLog> AiUsageLogs { get; set; } = new List<AiUsageLog>();

    [InverseProperty("Domain")]
    public virtual ICollection<Page> Pages { get; set; } = new List<Page>();

    [InverseProperty("Domain")]
    public virtual ICollection<ScanReport> ScanReports { get; set; } = new List<ScanReport>();

    [ForeignKey("UserId")]
    [InverseProperty("Domains")]
    public virtual AppUser User { get; set; } = null!;

    [InverseProperty("Domain")]
    public virtual ICollection<WidgetSetting> WidgetSettings { get; set; } = new List<WidgetSetting>();
}

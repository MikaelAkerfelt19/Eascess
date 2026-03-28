using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("DomainId", "RequestDate", Name = "IX_AiUsageLogs_DomainId_RequestDate")]
public partial class AiUsageLog
{
    [Key]
    public int Id { get; set; }

    public int DomainId { get; set; }

    public string? ScannedImageUrl { get; set; }

    public string? GeneratedAltText { get; set; }

    public int? TokensUsed { get; set; }

    public DateTime RequestDate { get; set; }

    [ForeignKey("DomainId")]
    [InverseProperty("AiUsageLogs")]
    public virtual Domain Domain { get; set; } = null!;
}

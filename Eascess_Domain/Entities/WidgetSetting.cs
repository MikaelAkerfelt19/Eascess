using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("DomainId", "VersionNumber", Name = "UQ_WidgetSettings_Version", IsUnique = true)]
public partial class WidgetSetting
{
    [Key]
    public int Id { get; set; }

    public int DomainId { get; set; }

    [StringLength(50)]
    public string? ThemeColor { get; set; }

    [StringLength(50)]
    public string? Position { get; set; }

    [StringLength(10)]
    public string? Language { get; set; }

    public bool IsAiEnabled { get; set; }

    public int VersionNumber { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsActive { get; set; }

    [ForeignKey("DomainId")]
    [InverseProperty("WidgetSettings")]
    public virtual Domain Domain { get; set; } = null!;
}

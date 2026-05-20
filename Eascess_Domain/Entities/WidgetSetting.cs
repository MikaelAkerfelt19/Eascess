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

    /// <summary>Panel başlığında gösterilecek logo URL'si (https:// zorunlu)</summary>
    [StringLength(512)]
    public string? LogoUrl { get; set; }

    /// <summary>"Erişilebilirlik" yerine özel başlık — maks 30 karakter</summary>
    [StringLength(30)]
    public string? WidgetTitle { get; set; }

    /// <summary>False → "Powered by Eascess" footer'ı gizle (Pro plan)</summary>
    public bool PoweredByVisible { get; set; } = true;

    [ForeignKey("DomainId")]
    [InverseProperty("WidgetSettings")]
    public virtual Domain Domain { get; set; } = null!;
}

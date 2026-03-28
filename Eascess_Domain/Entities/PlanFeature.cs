using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("PlanId", Name = "IX_PlanFeatures_PlanId")]
public partial class PlanFeature
{
    [Key]
    public int Id { get; set; }

    public int PlanId { get; set; }

    [StringLength(100)]
    public string FeatureKey { get; set; } = null!;

    public int? FeatureValueInt { get; set; }

    [StringLength(255)]
    public string? FeatureValueString { get; set; }

    [ForeignKey("PlanId")]
    [InverseProperty("PlanFeatures")]
    public virtual Plan Plan { get; set; } = null!;
}

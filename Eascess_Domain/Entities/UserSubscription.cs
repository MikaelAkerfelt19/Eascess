using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

public partial class UserSubscription
{
    [Key]
    public int Id { get; set; }

    [StringLength(450)]
    public string UserId { get; set; } = null!;

    public int PlanId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [StringLength(255)]
    public string? PaymentProviderSubscriptionId { get; set; }

    public bool AutoRenew { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? CanceledAt { get; set; }

    [InverseProperty("Subscription")]
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    [ForeignKey("PlanId")]
    [InverseProperty("UserSubscriptions")]
    public virtual Plan Plan { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserSubscriptions")]
    public virtual AppUser User { get; set; } = null!;
}

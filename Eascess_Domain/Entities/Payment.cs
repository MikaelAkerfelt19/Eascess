using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

public partial class Payment
{
    [Key]
    public int Id { get; set; }

    [StringLength(450)]
    public string UserId { get; set; } = null!;

    public int SubscriptionId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = null!;

    public DateTime PaymentDate { get; set; }

    [StringLength(50)]
    public string? PaymentProvider { get; set; }

    [StringLength(50)]
    public string? PaymentStatus { get; set; }

    [StringLength(255)]
    public string? TransactionId { get; set; }

    public string? RawResponse { get; set; }

    [InverseProperty("Payment")]
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    [ForeignKey("SubscriptionId")]
    [InverseProperty("Payments")]
    public virtual UserSubscription Subscription { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("Payments")]
    public virtual AppUser User { get; set; } = null!;
}

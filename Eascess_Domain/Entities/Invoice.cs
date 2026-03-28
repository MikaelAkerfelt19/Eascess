using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("PaymentId", Name = "IX_Invoices_PaymentId")]
[Index("UserId", Name = "IX_Invoices_UserId")]
public partial class Invoice
{
    [Key]
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public int? PaymentId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = null!;

    public bool IsPaid { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    [ForeignKey("PaymentId")]
    [InverseProperty("Invoices")]
    public virtual Payment? Payment { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Invoices")]
    public virtual AppUser User { get; set; } = null!;
}

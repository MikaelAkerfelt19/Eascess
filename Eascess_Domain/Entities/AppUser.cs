using Microsoft.AspNetCore.Identity;

namespace Eascess_Domain.Entities;

public class AppUser : IdentityUser
{
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public virtual ICollection<Domain> Domains { get; set; } = new List<Domain>();
    public virtual ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>(); // ← eksikti
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("UserId", "CreatedAt", Name = "IX_SupportTickets_User_Date")]
public class SupportTicket
{
    [Key]
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public int? DomainId { get; set; }

    [StringLength(200)]
    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    /// <summary>Open | Closed</summary>
    [StringLength(20)]
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("SupportTickets")]
    public virtual AppUser User { get; set; } = null!;

    [ForeignKey("DomainId")]
    public virtual Domain? Domain { get; set; }
}

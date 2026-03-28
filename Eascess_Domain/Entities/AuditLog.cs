using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Index("EntityType", "EntityId", Name = "IX_AuditLogs_EntityType_EntityId")]
[Index("UserId", "CreatedAt", Name = "IX_AuditLogs_UserId_CreatedAt", IsDescending = new[] { false, true })]
public partial class AuditLog
{
    [Key]
    public long Id { get; set; }

    public string? UserId { get; set; }

    [StringLength(100)]
    public string Action { get; set; } = null!;

    [StringLength(100)]
    public string EntityType { get; set; } = null!;

    [StringLength(100)]
    public string? EntityId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    [StringLength(50)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("AuditLogs")]
    public virtual AppUser? User { get; set; }
}

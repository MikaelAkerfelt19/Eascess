using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Table("AiProcessingQueue")]
public partial class AiProcessingQueue
{
    [Key]
    public int Id { get; set; }

    public int DomainId { get; set; }

    public string ImageUrl { get; set; } = null!;

    [StringLength(50)]
    public string? Status { get; set; }

    public int? RetryCount { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("DomainId")]
    [InverseProperty("AiProcessingQueues")]
    public virtual Domain Domain { get; set; } = null!;
}

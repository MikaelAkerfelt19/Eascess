using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

[Table("ImageAltTextCache")]
[Index("UrlHash", Name = "IX_ImageAltTextCache_UrlHash")]
[Index("DomainId", "UrlHash", Name = "UQ_ImageAltTextCache_Domain_UrlHash", IsUnique = true)]
public class ImageAltTextCache
{
    [Key]
    public int Id { get; set; }

    public int DomainId { get; set; }

    [StringLength(64)]
    public string UrlHash { get; set; } = null!;

    public string OriginalUrl { get; set; } = null!;

    public string AltText { get; set; } = string.Empty;

    [StringLength(50)]
    public string Provider { get; set; } = "Gemini";

    public DateTime GeneratedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    [ForeignKey("DomainId")]
    [InverseProperty("ImageAltTextCaches")]
    public virtual Domain Domain { get; set; } = null!;
}

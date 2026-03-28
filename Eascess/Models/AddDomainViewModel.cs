using System.ComponentModel.DataAnnotations;

namespace Eascess.Models;

public class AddDomainViewModel
{
    [Required(ErrorMessage = "Domain URL zorunludur.")]
    [StringLength(255)]
    [RegularExpression(
        @"^(https?://)?([a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$",
        ErrorMessage = "Geçerli bir domain adresi girin. (örn: example.com veya https://example.com)")]
    public string DomainUrl { get; set; } = string.Empty;
}

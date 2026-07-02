namespace Eascess.Models;

public class DomainListItemViewModel
{
    public int Id { get; set; }
    public string DomainUrl { get; set; } = string.Empty;
    public Guid LicenseKey { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Widget'ın sunulduğu uygulama kökü — controller tarafından Request'ten doldurulur.</summary>
    public string AppBaseUrl { get; set; } = "https://app.eascess.io";

    /// <summary>Müşterinin sitesine yapıştıracağı script etiketi</summary>
    public string ScriptTag => $"<script src=\"{AppBaseUrl}/js/widget.js\" data-key=\"{LicenseKey}\" data-api=\"{AppBaseUrl}\" defer></script>";
}

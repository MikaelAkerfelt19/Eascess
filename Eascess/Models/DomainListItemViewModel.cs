namespace Eascess.Models;

public class DomainListItemViewModel
{
    public int Id { get; set; }
    public string DomainUrl { get; set; } = string.Empty;
    public Guid LicenseKey { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Müşterinin sitesine yapıştıracağı script etiketi</summary>
    public string ScriptTag => $"<script src=\"https://cdn.eascess.io/widget.js\" data-key=\"{LicenseKey}\" data-api=\"https://app.eascess.io\" defer></script>";
}

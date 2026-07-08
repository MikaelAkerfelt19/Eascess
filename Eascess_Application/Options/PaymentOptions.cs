namespace Eascess_Application.Options;

/// <summary>
/// Ödeme yapılandırması. Tüm gizli anahtarlar YALNIZCA yapılandırmadan
/// (user-secrets / ortam değişkeni / Azure App Settings) okunur.
/// appsettings.json'daki karşılıkları BOŞ bırakılmalıdır — repoya sır girmez.
///
/// Ortam değişkeni karşılıkları (çift alt çizgi bölüm ayırıcıdır):
///   Payments__Provider
///   Payments__ApiKey
///   Payments__SecretKey
///   Payments__BaseUrl
///   Payments__CallbackUrl
/// </summary>
public class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// Etkin sağlayıcı adı. "Sandbox" gerçek tahsilat yapmaz — geliştirme
    /// ve test içindir. Gerçek sağlayıcıya geçmek için bu değeri değiştirin.
    /// </summary>
    public string Provider { get; set; } = "Sandbox";

    /// <summary>Sağlayıcı API anahtarı. appsettings.json'da BOŞ kalır.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Sağlayıcı gizli anahtarı — callback imzası bununla doğrulanır. BOŞ kalır.</summary>
    public string SecretKey { get; set; } = "";

    /// <summary>Sağlayıcı API kök adresi, ör. https://sandbox-api.saglayici.com</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// Sağlayıcının geri döneceği mutlak URL. Boş bırakılırsa isteğin kendi
    /// host'undan /Checkout/Callback olarak üretilir (yerel geliştirme için).
    /// Üretimde açıkça ayarlanmalıdır (proxy arkasında host yanlış çözülebilir).
    /// </summary>
    public string CallbackUrl { get; set; } = "";

    /// <summary>Sağlayıcı çağrılarında zaman aşımı.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Yalnızca Sandbox sağlayıcısı için: ödeme onay ekranını atlayıp
    /// doğrudan sonuç üretir. Otomatik testlerde kullanılır.
    /// </summary>
    public bool SandboxAutoApprove { get; set; }
}

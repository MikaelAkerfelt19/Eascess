namespace Eascess_Application.DTOs.Payments;

/// <summary>
/// Sağlayıcının callback isteğinin ham hâli.
///
/// İmza doğrulaması genellikle ham gövde (RawBody) üzerinden yapılır — gövdeyi
/// deserialize edip yeniden serialize etmek imzayı bozar. Bu yüzden gövde
/// DEĞİŞTİRİLMEDEN buraya taşınır ve doğrulama sağlayıcı adaptöründe yapılır.
/// </summary>
public class PaymentCallbackContext
{
    /// <summary>İstek gövdesi, hiç dokunulmamış hâliyle.</summary>
    public string RawBody { get; set; } = "";

    /// <summary>İstek başlıkları — imza çoğu sağlayıcıda burada gelir.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; set; } =
        new Dictionary<string, string>();

    /// <summary>Form alanları (application/x-www-form-urlencoded gönderen sağlayıcılar için).</summary>
    public IReadOnlyDictionary<string, string> Form { get; set; } =
        new Dictionary<string, string>();

    /// <summary>Query string parametreleri (GET ile dönen sağlayıcılar için).</summary>
    public IReadOnlyDictionary<string, string> Query { get; set; } =
        new Dictionary<string, string>();
}

namespace Eascess_Domain.Constants;

/// <summary>
/// Türkiye'nin 81 ili — plaka koduna göre sıralı.
///
/// Fatura ülkesi Türkiye olduğunda şehir alanı serbest metin değil, bu listeden
/// seçim olur; böylece fatura adresindeki il bilgisi tutarlı yazılır.
/// Diğer ülkelerde şehir serbest metin olarak alınır.
/// </summary>
public static class TurkeyProvinces
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Adana", "Adıyaman", "Afyonkarahisar", "Ağrı", "Amasya", "Ankara", "Antalya",
        "Artvin", "Aydın", "Balıkesir", "Bilecik", "Bingöl", "Bitlis", "Bolu",
        "Burdur", "Bursa", "Çanakkale", "Çankırı", "Çorum", "Denizli", "Diyarbakır",
        "Edirne", "Elazığ", "Erzincan", "Erzurum", "Eskişehir", "Gaziantep", "Giresun",
        "Gümüşhane", "Hakkâri", "Hatay", "Isparta", "Mersin", "İstanbul", "İzmir",
        "Kars", "Kastamonu", "Kayseri", "Kırklareli", "Kırşehir", "Kocaeli", "Konya",
        "Kütahya", "Malatya", "Manisa", "Kahramanmaraş", "Mardin", "Muğla", "Muş",
        "Nevşehir", "Niğde", "Ordu", "Rize", "Sakarya", "Samsun", "Siirt", "Sinop",
        "Sivas", "Tekirdağ", "Tokat", "Trabzon", "Tunceli", "Şanlıurfa", "Uşak",
        "Van", "Yozgat", "Zonguldak", "Aksaray", "Bayburt", "Karaman", "Kırıkkale",
        "Batman", "Şırnak", "Bartın", "Ardahan", "Iğdır", "Yalova", "Karabük",
        "Kilis", "Osmaniye", "Düzce",
    };

    /// <summary>Alfabetik (Türkçe) sıralı liste — açılır listede bu sıra kullanılır.</summary>
    public static readonly IReadOnlyList<string> Alphabetical =
        All.OrderBy(p => p, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), ignoreCase: false))
           .ToList();

    private static readonly HashSet<string> Lookup = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? province) =>
        province is not null && Lookup.Contains(province.Trim());
}

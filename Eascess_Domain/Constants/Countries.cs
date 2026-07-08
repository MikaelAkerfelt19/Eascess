namespace Eascess_Domain.Constants;

/// <summary>
/// Fatura ülkesi. Telefon kodu ve KDV oranı bu kayıttan okunur.
/// </summary>
/// <param name="Code">ISO 3166-1 alpha-2 kodu — siparişte saklanan değer budur.</param>
/// <param name="Name">Türkçe ülke adı (ekranda gösterilir).</param>
/// <param name="DialCode">Uluslararası arama kodu, "+" olmadan.</param>
/// <param name="VatRate">Standart KDV/GST oranı. Ulusal KDV'si olmayan ülkelerde 0.</param>
/// <param name="PhoneDigits">
/// Ulusal numaranın (ülke kodu hariç) hane sayısı. 0 ise ülkeye özel bir kural
/// yoktur ve genel aralık (<see cref="MinPhoneDigits"/>–<see cref="MaxPhoneDigits"/>) uygulanır.
/// </param>
public sealed record Country(string Code, string Name, string DialCode, decimal VatRate, int PhoneDigits = 0);

/// <summary>
/// Ülke listesi ve ülkeye bağlı faturalama kuralları.
///
/// KDV NOTU: Burada her ülkenin STANDART KDV/GST oranı tutulur ve sipariş
/// tutarı bu oranla hesaplanır. Bu, "müşterinin ülkesinin oranı uygulanır"
/// kuralıdır (AB'de dijital hizmetler için geçerli yaklaşım). İki durum
/// KAPSAM DIŞIDIR ve gerekirse ayrıca ele alınmalıdır:
///   • AB içi B2B "reverse charge" (geçerli VAT numarasıyla %0),
///   • ABD gibi eyalet bazlı satış vergisi olan ülkeler (burada 0 kabul edilir).
/// Oranlar 2024 standart oranlarıdır; mali mevzuat değiştiğinde güncellenmelidir.
/// </summary>
public static class Countries
{
    public const string DefaultCode = "TR";

    /// <summary>Ülkeye özel kural yoksa kabul edilen ulusal numara uzunluğu aralığı.</summary>
    public const int MinPhoneDigits = 6;
    public const int MaxPhoneDigits = 14;

    /// <summary>
    /// Tüm ülkeler. Türkiye ilk sırada (varsayılan pazar), kalanlar Türkçe
    /// alfabetik. Türkiye için ulusal numara tam 10 hanedir (5xx xxx xx xx).
    /// </summary>
    public static readonly IReadOnlyList<Country> All = new[]
    {
        new Country("TR", "Türkiye", "90", 0.20m, PhoneDigits: 10),

        new Country("AF", "Afganistan", "93", 0.10m),
        new Country("DE", "Almanya", "49", 0.19m),
        new Country("US", "Amerika Birleşik Devletleri", "1", 0.00m),
        new Country("AD", "Andorra", "376", 0.045m),
        new Country("AO", "Angola", "244", 0.14m),
        new Country("AG", "Antigua ve Barbuda", "1268", 0.15m),
        new Country("AR", "Arjantin", "54", 0.21m),
        new Country("AL", "Arnavutluk", "355", 0.20m),
        new Country("AU", "Avustralya", "61", 0.10m),
        new Country("AT", "Avusturya", "43", 0.20m),
        new Country("AZ", "Azerbaycan", "994", 0.18m),
        new Country("BS", "Bahamalar", "1242", 0.10m),
        new Country("BH", "Bahreyn", "973", 0.10m),
        new Country("BD", "Bangladeş", "880", 0.15m),
        new Country("BB", "Barbados", "1246", 0.175m),
        new Country("BY", "Belarus", "375", 0.20m),
        new Country("BE", "Belçika", "32", 0.21m),
        new Country("BZ", "Belize", "501", 0.125m),
        new Country("BJ", "Benin", "229", 0.18m),
        new Country("AE", "Birleşik Arap Emirlikleri", "971", 0.05m),
        new Country("GB", "Birleşik Krallık", "44", 0.20m),
        new Country("BO", "Bolivya", "591", 0.13m),
        new Country("BA", "Bosna-Hersek", "387", 0.17m),
        new Country("BW", "Botsvana", "267", 0.14m),
        new Country("BR", "Brezilya", "55", 0.17m),
        new Country("BN", "Brunei", "673", 0.00m),
        new Country("BG", "Bulgaristan", "359", 0.20m),
        new Country("BF", "Burkina Faso", "226", 0.18m),
        new Country("BI", "Burundi", "257", 0.18m),
        new Country("BT", "Butan", "975", 0.07m),
        new Country("CV", "Cabo Verde", "238", 0.15m),
        new Country("DZ", "Cezayir", "213", 0.19m),
        new Country("DJ", "Cibuti", "253", 0.10m),
        new Country("TD", "Çad", "235", 0.18m),
        new Country("CZ", "Çekya", "420", 0.21m),
        new Country("CN", "Çin", "86", 0.13m),
        new Country("DK", "Danimarka", "45", 0.25m),
        new Country("DO", "Dominik Cumhuriyeti", "1809", 0.18m),
        new Country("DM", "Dominika", "1767", 0.15m),
        new Country("EC", "Ekvador", "593", 0.15m),
        new Country("GQ", "Ekvator Ginesi", "240", 0.15m),
        new Country("SV", "El Salvador", "503", 0.13m),
        new Country("ID", "Endonezya", "62", 0.11m),
        new Country("ER", "Eritre", "291", 0.05m),
        new Country("AM", "Ermenistan", "374", 0.20m),
        new Country("EE", "Estonya", "372", 0.22m),
        new Country("SZ", "Esvatini", "268", 0.15m),
        new Country("ET", "Etiyopya", "251", 0.15m),
        new Country("MA", "Fas", "212", 0.20m),
        new Country("FJ", "Fiji", "679", 0.15m),
        new Country("CI", "Fildişi Sahili", "225", 0.18m),
        new Country("PH", "Filipinler", "63", 0.12m),
        new Country("PS", "Filistin", "970", 0.16m),
        new Country("FI", "Finlandiya", "358", 0.255m),
        new Country("FR", "Fransa", "33", 0.20m),
        new Country("GA", "Gabon", "241", 0.18m),
        new Country("GM", "Gambiya", "220", 0.15m),
        new Country("GH", "Gana", "233", 0.15m),
        new Country("GN", "Gine", "224", 0.18m),
        new Country("GW", "Gine-Bissau", "245", 0.15m),
        new Country("GD", "Grenada", "1473", 0.15m),
        new Country("GL", "Grönland", "299", 0.00m),
        new Country("GT", "Guatemala", "502", 0.12m),
        new Country("GY", "Guyana", "592", 0.14m),
        new Country("ZA", "Güney Afrika", "27", 0.15m),
        new Country("KR", "Güney Kore", "82", 0.10m),
        new Country("SS", "Güney Sudan", "211", 0.18m),
        new Country("GE", "Gürcistan", "995", 0.18m),
        new Country("HT", "Haiti", "509", 0.10m),
        new Country("HR", "Hırvatistan", "385", 0.25m),
        new Country("IN", "Hindistan", "91", 0.18m),
        new Country("NL", "Hollanda", "31", 0.21m),
        new Country("HN", "Honduras", "504", 0.15m),
        new Country("HK", "Hong Kong", "852", 0.00m),
        new Country("IQ", "Irak", "964", 0.00m),
        new Country("IR", "İran", "98", 0.09m),
        new Country("IE", "İrlanda", "353", 0.23m),
        new Country("ES", "İspanya", "34", 0.21m),
        new Country("IL", "İsrail", "972", 0.17m),
        new Country("SE", "İsveç", "46", 0.25m),
        new Country("CH", "İsviçre", "41", 0.081m),
        new Country("IT", "İtalya", "39", 0.22m),
        new Country("IS", "İzlanda", "354", 0.24m),
        new Country("JM", "Jamaika", "1876", 0.15m),
        new Country("JP", "Japonya", "81", 0.10m),
        new Country("KH", "Kamboçya", "855", 0.10m),
        new Country("CM", "Kamerun", "237", 0.1925m),
        new Country("CA", "Kanada", "1", 0.05m),
        new Country("ME", "Karadağ", "382", 0.21m),
        new Country("QA", "Katar", "974", 0.00m),
        new Country("KZ", "Kazakistan", "7", 0.12m),
        new Country("KE", "Kenya", "254", 0.16m),
        new Country("CY", "Kıbrıs", "357", 0.19m),
        new Country("KG", "Kırgızistan", "996", 0.12m),
        new Country("KI", "Kiribati", "686", 0.125m),
        new Country("CO", "Kolombiya", "57", 0.19m),
        new Country("KM", "Komorlar", "269", 0.10m),
        new Country("CG", "Kongo Cumhuriyeti", "242", 0.16m),
        new Country("CD", "Kongo Demokratik Cumhuriyeti", "243", 0.16m),
        new Country("XK", "Kosova", "383", 0.18m),
        new Country("CR", "Kosta Rika", "506", 0.13m),
        new Country("KW", "Kuveyt", "965", 0.00m),
        new Country("KP", "Kuzey Kore", "850", 0.00m),
        new Country("MK", "Kuzey Makedonya", "389", 0.18m),
        new Country("CU", "Küba", "53", 0.00m),
        new Country("LA", "Laos", "856", 0.10m),
        new Country("LS", "Lesotho", "266", 0.15m),
        new Country("LV", "Letonya", "371", 0.21m),
        new Country("LR", "Liberya", "231", 0.10m),
        new Country("LY", "Libya", "218", 0.00m),
        new Country("LI", "Liechtenstein", "423", 0.081m),
        new Country("LT", "Litvanya", "370", 0.21m),
        new Country("LB", "Lübnan", "961", 0.11m),
        new Country("LU", "Lüksemburg", "352", 0.17m),
        new Country("HU", "Macaristan", "36", 0.27m),
        new Country("MG", "Madagaskar", "261", 0.20m),
        new Country("MW", "Malavi", "265", 0.165m),
        new Country("MV", "Maldivler", "960", 0.08m),
        new Country("MY", "Malezya", "60", 0.06m),
        new Country("ML", "Mali", "223", 0.18m),
        new Country("MT", "Malta", "356", 0.18m),
        new Country("MH", "Marshall Adaları", "692", 0.00m),
        new Country("MX", "Meksika", "52", 0.16m),
        new Country("EG", "Mısır", "20", 0.14m),
        new Country("FM", "Mikronezya", "691", 0.00m),
        new Country("MN", "Moğolistan", "976", 0.10m),
        new Country("MD", "Moldova", "373", 0.20m),
        new Country("MC", "Monako", "377", 0.20m),
        new Country("MR", "Moritanya", "222", 0.16m),
        new Country("MU", "Mauritius", "230", 0.15m),
        new Country("MZ", "Mozambik", "258", 0.16m),
        new Country("MM", "Myanmar", "95", 0.05m),
        new Country("NA", "Namibya", "264", 0.15m),
        new Country("NR", "Nauru", "674", 0.00m),
        new Country("NP", "Nepal", "977", 0.13m),
        new Country("NE", "Nijer", "227", 0.19m),
        new Country("NG", "Nijerya", "234", 0.075m),
        new Country("NI", "Nikaragua", "505", 0.15m),
        new Country("NO", "Norveç", "47", 0.25m),
        new Country("CF", "Orta Afrika Cumhuriyeti", "236", 0.19m),
        new Country("UZ", "Özbekistan", "998", 0.12m),
        new Country("PK", "Pakistan", "92", 0.18m),
        new Country("PW", "Palau", "680", 0.10m),
        new Country("PA", "Panama", "507", 0.07m),
        new Country("PG", "Papua Yeni Gine", "675", 0.10m),
        new Country("PY", "Paraguay", "595", 0.10m),
        new Country("PE", "Peru", "51", 0.18m),
        new Country("PL", "Polonya", "48", 0.23m),
        new Country("PT", "Portekiz", "351", 0.23m),
        new Country("RO", "Romanya", "40", 0.19m),
        new Country("RW", "Ruanda", "250", 0.18m),
        new Country("RU", "Rusya", "7", 0.20m),
        new Country("WS", "Samoa", "685", 0.15m),
        new Country("ST", "São Tomé ve Príncipe", "239", 0.15m),
        new Country("SN", "Senegal", "221", 0.18m),
        new Country("SC", "Seyşeller", "248", 0.15m),
        new Country("RS", "Sırbistan", "381", 0.20m),
        new Country("SL", "Sierra Leone", "232", 0.15m),
        new Country("SG", "Singapur", "65", 0.09m),
        new Country("SK", "Slovakya", "421", 0.23m),
        new Country("SI", "Slovenya", "386", 0.22m),
        new Country("SB", "Solomon Adaları", "677", 0.15m),
        new Country("SO", "Somali", "252", 0.05m),
        new Country("LK", "Sri Lanka", "94", 0.18m),
        new Country("SD", "Sudan", "249", 0.17m),
        new Country("SR", "Surinam", "597", 0.10m),
        new Country("SA", "Suudi Arabistan", "966", 0.15m),
        new Country("SY", "Suriye", "963", 0.00m),
        new Country("CL", "Şili", "56", 0.19m),
        new Country("TJ", "Tacikistan", "992", 0.15m),
        new Country("TZ", "Tanzanya", "255", 0.18m),
        new Country("TH", "Tayland", "66", 0.07m),
        new Country("TW", "Tayvan", "886", 0.05m),
        new Country("TG", "Togo", "228", 0.18m),
        new Country("TO", "Tonga", "676", 0.15m),
        new Country("TT", "Trinidad ve Tobago", "1868", 0.125m),
        new Country("TN", "Tunus", "216", 0.19m),
        new Country("TM", "Türkmenistan", "993", 0.15m),
        new Country("TV", "Tuvalu", "688", 0.00m),
        new Country("UG", "Uganda", "256", 0.18m),
        new Country("UA", "Ukrayna", "380", 0.20m),
        new Country("OM", "Umman", "968", 0.05m),
        new Country("UY", "Uruguay", "598", 0.22m),
        new Country("JO", "Ürdün", "962", 0.16m),
        new Country("VU", "Vanuatu", "678", 0.15m),
        new Country("VA", "Vatikan", "379", 0.00m),
        new Country("VE", "Venezuela", "58", 0.16m),
        new Country("VN", "Vietnam", "84", 0.10m),
        new Country("YE", "Yemen", "967", 0.05m),
        new Country("NC", "Yeni Kaledonya", "687", 0.11m),
        new Country("NZ", "Yeni Zelanda", "64", 0.15m),
        new Country("CV", "Yeşil Burun Adaları", "238", 0.15m),
        new Country("GR", "Yunanistan", "30", 0.24m),
        new Country("ZM", "Zambiya", "260", 0.16m),
        new Country("ZW", "Zimbabve", "263", 0.15m),
    }
    // Aynı ISO kodunun iki kez yazılması (ör. Cabo Verde / Yeşil Burun Adaları)
    // arama sözlüğünü bozar; ilk kayıt korunur.
    .GroupBy(c => c.Code)
    .Select(g => g.First())
    .ToList();

    private static readonly Dictionary<string, Country> ByCode =
        All.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    public static Country Default => ByCode[DefaultCode];

    /// <summary>Kodu bilinmeyen ülke için null döner — çağıran doğrulamada reddeder.</summary>
    public static Country? Find(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var country) ? country : null;

    public static bool IsValid(string? code) => Find(code) is not null;

    /// <summary>Bilinmeyen kodda varsayılan ülkenin oranına düşer.</summary>
    public static decimal VatRateFor(string? code) => (Find(code) ?? Default).VatRate;

    public static string NameFor(string? code) => (Find(code) ?? Default).Name;

    public static string DialCodeFor(string? code) => (Find(code) ?? Default).DialCode;
}

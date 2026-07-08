using Eascess.Models;
using Eascess_Domain.Constants;

namespace Eascess.Tests.Unit;

/// <summary>
/// Fatura kuralları: ülke listesi, ülkeye göre KDV, telefon ve e-posta
/// doğrulaması, il listesi ve panel selamlaması.
/// </summary>
public class BillingRulesTests
{
    // ── Ülke listesi ───────────────────────────────────────────────

    [Fact]
    public void Countries_TurkiyeIlkSirada_VarsayilanUlke()
    {
        Assert.Equal("TR", Countries.All[0].Code);
        Assert.Equal("TR", Countries.DefaultCode);
        Assert.Equal("Türkiye", Countries.Default.Name);
    }

    [Fact]
    public void Countries_KodlarBenzersiz_SozlukBozulmaz()
    {
        // Aynı ISO kodu iki kez yazılırsa (ör. Cabo Verde / Yeşil Burun Adaları)
        // liste tekilleştirilir; aksi hâlde sözlük kurulumu patlar.
        var duplicates = Countries.All
            .GroupBy(c => c.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Countries_GenisKapsamli_YuzdenFazlaUlke()
    {
        Assert.True(Countries.All.Count > 150, $"Beklenen >150, bulunan {Countries.All.Count}");
    }

    [Fact]
    public void Countries_TumKayitlarGecerli_KodVeAramaKoduDolu()
    {
        Assert.All(Countries.All, c =>
        {
            Assert.Equal(2, c.Code.Length);
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.Matches(@"^\d+$", c.DialCode);
            Assert.InRange(c.VatRate, 0m, 0.30m);
        });
    }

    // ── Ülkeye göre KDV ────────────────────────────────────────────

    [Theory]
    [InlineData("TR", 0.20)]  // Türkiye
    [InlineData("DE", 0.19)]  // Almanya
    [InlineData("HU", 0.27)]  // Macaristan — en yüksek
    [InlineData("AE", 0.05)]  // BAE
    [InlineData("US", 0.00)]  // ABD — ulusal KDV yok
    public void VatRateFor_UlkeyeGoreDogruOran(string code, double expected)
    {
        Assert.Equal((decimal)expected, Countries.VatRateFor(code));
    }

    [Fact]
    public void VatRateFor_BilinmeyenKod_VarsayilanOrananDuser()
    {
        Assert.Equal(BillingPolicy.DefaultTaxRate, Countries.VatRateFor("ZZ"));
        Assert.Equal(BillingPolicy.DefaultTaxRate, Countries.VatRateFor(null));
    }

    // ── İl listesi ─────────────────────────────────────────────────

    [Fact]
    public void TurkeyProvinces_SeksenBirIl()
    {
        Assert.Equal(81, TurkeyProvinces.All.Count);
        Assert.Equal(81, TurkeyProvinces.Alphabetical.Count);
    }

    [Theory]
    [InlineData("İstanbul", true)]
    [InlineData("Ankara", true)]
    [InlineData("Düzce", true)]
    [InlineData("  İzmir  ", true)]   // kırpılır
    [InlineData("Berlin", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TurkeyProvinces_IsValid(string? province, bool expected)
    {
        Assert.Equal(expected, TurkeyProvinces.IsValid(province));
    }

    // ── Telefon ────────────────────────────────────────────────────

    [Theory]
    [InlineData("+90 555 123 45 67")]
    [InlineData("90 555 123 45 67")]
    [InlineData("0555 123 45 67")]
    [InlineData("555 123 45 67")]
    [InlineData("(0555) 123-45-67")]
    public void ValidatePhone_TurkiyeFarkliYazimlar_AyniNormalizeSonuc(string input)
    {
        var result = BillingContactRules.ValidatePhone(input, "TR");

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("+905551234567", result.Normalized);
    }

    [Theory]
    [InlineData("555 123 45")]        // 8 hane — eksik
    [InlineData("555 123 45 67 89")]  // 12 hane — fazla
    [InlineData("")]
    [InlineData("abc")]
    public void ValidatePhone_TurkiyeYanlisUzunluk_Reddedilir(string input)
    {
        var result = BillingContactRules.ValidatePhone(input, "TR");

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ValidatePhone_TurkiyeIcinTamOnHaneSarti_HataMesajiAciklayici()
    {
        var result = BillingContactRules.ValidatePhone("5551234", "TR");

        Assert.False(result.IsValid);
        Assert.Contains("+90", result.ErrorMessage);
        Assert.Contains("10", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePhone_BaskaUlke_KendiUlkeKoduIleNormalizeEdilir()
    {
        // Almanya: sabit uzunluk kuralı yok, genel aralık uygulanır.
        var result = BillingContactRules.ValidatePhone("030 1234567", "DE");

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.StartsWith("+49", result.Normalized);
        Assert.Equal("+49301234567", result.Normalized);
    }

    [Fact]
    public void ValidatePhone_UlkeKoduYazilmissa_TekrarEklenmez()
    {
        var result = BillingContactRules.ValidatePhone("+49 30 1234567", "DE");

        Assert.Equal("+49301234567", result.Normalized);
    }

    // ── E-posta ────────────────────────────────────────────────────

    [Theory]
    [InlineData("ada@example.com")]
    [InlineData("ada.lovelace@alt.alanadi.com.tr")]
    [InlineData("ada+etiket@example.co")]
    [InlineData("a1@b2.io")]
    public void IsValidEmail_GecerliAdresler(string email)
    {
        Assert.True(BillingContactRules.IsValidEmail(email));
    }

    [Theory]
    [InlineData("ada.example.com")]     // @ yok
    [InlineData("ada@@example.com")]    // iki @
    [InlineData("ada@b@c.com")]         // iki @
    [InlineData("ada@localhost")]       // nokta yok
    [InlineData("ada@example.c")]       // tek harfli uzantı
    [InlineData("@example.com")]        // yerel kısım yok
    [InlineData("ada@")]                // alan adı yok
    [InlineData("ada..soyad@example.com")] // art arda nokta
    [InlineData(".ada@example.com")]    // nokta ile başlıyor
    [InlineData("")]
    [InlineData(null)]
    public void IsValidEmail_GecersizAdresler(string? email)
    {
        Assert.False(BillingContactRules.IsValidEmail(email));
    }

    [Fact]
    public void NormalizeEmail_KucukHarfeVeKirpmaya()
    {
        Assert.Equal("ada@example.com", BillingContactRules.NormalizeEmail("  Ada@Example.COM "));
    }

    // ── Selamlama ──────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  "İyi geceler")]
    [InlineData(3,  "İyi geceler")]
    [InlineData(5,  "İyi geceler")]
    [InlineData(6,  "Günaydın")]
    [InlineData(9,  "Günaydın")]
    [InlineData(11, "Günaydın")]
    [InlineData(12, "Merhabalar")]
    [InlineData(15, "Merhabalar")]
    [InlineData(18, "Merhabalar")]
    [InlineData(19, "İyi akşamlar")]
    [InlineData(22, "İyi akşamlar")]
    [InlineData(23, "İyi akşamlar")]
    public void Greeting_SaatDilimleri(int hour, string expected)
    {
        Assert.Equal(expected, Greeting.ForHour(hour));
    }

    [Fact]
    public void Greeting_TumSaatlerBirKarsilikVerir()
    {
        for (var h = 0; h < 24; h++)
            Assert.False(string.IsNullOrWhiteSpace(Greeting.ForHour(h)));
    }
}

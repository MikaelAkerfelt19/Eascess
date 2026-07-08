using System.ComponentModel.DataAnnotations;
using Eascess_Application.DTOs.Payments;
using Eascess_Domain.Entities;

namespace Eascess.Models;

/// <summary>
/// Ödeme ekranının form modeli.
///
/// DİKKAT: Burada TUTAR ALANI YOKTUR ve eklenmemelidir. Fiyat her zaman
/// PlanId üzerinden sunucuda hesaplanır; istemcinin gönderdiği bir tutara
/// güvenilmez. Aynı şekilde kupon KODU gelir, indirim tutarı gelmez.
/// </summary>
public class CheckoutFormModel : IValidatableObject
{
    public int PlanId { get; set; }

    /// <summary>"Monthly" | "Yearly"</summary>
    public string BillingPeriod { get; set; } = Eascess_Domain.Constants.BillingPeriods.Monthly;

    [StringLength(40, ErrorMessage = "Kupon kodu en fazla 40 karakter olabilir.")]
    public string? CouponCode { get; set; }

    /// <summary>
    /// Sayfa açılışında üretilen çift gönderim jetonu. Idempotency anahtarı
    /// bundan türetilir — aynı formun iki kez gönderilmesi çift tahsilat yaratmaz.
    /// </summary>
    public string ClientToken { get; set; } = "";

    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Ad soyad 3–200 karakter olmalıdır.")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [StringLength(256)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    /// <summary>
    /// Kullanıcının yazdığı hâliyle telefon. Biçim kontrolü ve normalizasyon
    /// ülkeye bağlı olduğu için Validate() içinde yapılır.
    /// </summary>
    [Required(ErrorMessage = "Telefon zorunludur.")]
    [StringLength(40)]
    [Display(Name = "Telefon")]
    public string Phone { get; set; } = "";

    /// <summary>ISO 3166-1 alpha-2 ülke kodu. KDV oranı buna göre belirlenir.</summary>
    [Required(ErrorMessage = "Ülke zorunludur.")]
    [StringLength(2, MinimumLength = 2)]
    [Display(Name = "Ülke")]
    public string Country { get; set; } = Eascess_Domain.Constants.Countries.DefaultCode;

    /// <summary>Türkiye'de il (81 il listesinden), diğer ülkelerde serbest metin şehir.</summary>
    [Required(ErrorMessage = "Şehir zorunludur.")]
    [StringLength(100)]
    [Display(Name = "Şehir")]
    public string City { get; set; } = "";

    [Required(ErrorMessage = "Adres zorunludur.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Adres en az 10 karakter olmalıdır.")]
    [Display(Name = "Adres")]
    public string Address { get; set; } = "";

    /// <summary>İşaretlenirse şirket alanları zorunlu hâle gelir.</summary>
    [Display(Name = "Kurumsal fatura istiyorum")]
    public bool IsCompany { get; set; }

    [StringLength(200)]
    [Display(Name = "Şirket Unvanı")]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    [Display(Name = "Vergi Dairesi")]
    public string? TaxOffice { get; set; }

    [StringLength(50)]
    [Display(Name = "Vergi / TC Kimlik No")]
    public string? TaxNumber { get; set; }

    [Display(Name = "Sözleşmeler")]
    public bool AcceptTerms { get; set; }

    /// <summary>
    /// Koşullu kurallar: kurumsal fatura seçiliyse şirket alanları zorunludur,
    /// sözleşme onayı her hâlükârda zorunludur.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Ülke — geçersizse KDV oranı da belirlenemez, önce bu kontrol edilir.
        var country = Eascess_Domain.Constants.Countries.Find(Country);
        if (country is null)
        {
            yield return new ValidationResult(
                "Lütfen listeden geçerli bir ülke seçin.", new[] { nameof(Country) });
        }
        else
        {
            // Türkiye'de şehir serbest metin değil, 81 il listesinden seçilir.
            if (country.Code == Eascess_Domain.Constants.Countries.DefaultCode &&
                !Eascess_Domain.Constants.TurkeyProvinces.IsValid(City))
            {
                yield return new ValidationResult(
                    "Lütfen listeden geçerli bir il seçin.", new[] { nameof(City) });
            }

            // Telefon kuralı ülkeye bağlıdır: Türkiye'de +90 sonrası tam 10 hane.
            var phone = Eascess_Domain.Constants.BillingContactRules.ValidatePhone(Phone, country.Code);
            if (!phone.IsValid)
                yield return new ValidationResult(phone.ErrorMessage!, new[] { nameof(Phone) });
        }

        if (!Eascess_Domain.Constants.BillingContactRules.IsValidEmail(Email))
        {
            yield return new ValidationResult(
                "Geçerli bir e-posta adresi girin (örnek: ad@alanadi.com).", new[] { nameof(Email) });
        }

        if (!AcceptTerms)
        {
            yield return new ValidationResult(
                "Devam etmek için mesafeli satış sözleşmesini onaylamanız gerekir.",
                new[] { nameof(AcceptTerms) });
        }

        if (!IsCompany)
            yield break;

        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            yield return new ValidationResult(
                "Kurumsal fatura için şirket unvanı zorunludur.",
                new[] { nameof(CompanyName) });
        }

        if (string.IsNullOrWhiteSpace(TaxOffice))
        {
            yield return new ValidationResult(
                "Kurumsal fatura için vergi dairesi zorunludur.",
                new[] { nameof(TaxOffice) });
        }

        if (string.IsNullOrWhiteSpace(TaxNumber))
        {
            yield return new ValidationResult(
                "Kurumsal fatura için vergi numarası zorunludur.",
                new[] { nameof(TaxNumber) });
        }
        else if (!TaxNumber.All(char.IsDigit) || TaxNumber.Length is < 10 or > 11)
        {
            yield return new ValidationResult(
                "Vergi numarası 10 haneli, TC kimlik numarası 11 haneli olmalıdır.",
                new[] { nameof(TaxNumber) });
        }
    }
}

/// <summary>Ödeme ekranına aktarılan tüm veri — sipariş özeti sunucuda hesaplanmıştır.</summary>
public class CheckoutViewModel
{
    public CheckoutFormModel Form { get; set; } = new();

    /// <summary>Sunucuda hesaplanan fiyat kırılımı. Ekranda gösterilen tek kaynak.</summary>
    public CheckoutQuote Quote { get; set; } = new();

    public Plan Plan { get; set; } = null!;

    /// <summary>Kullanıcının şu anki planı — "yükseltme" bilgisi için gösterilir.</summary>
    public string CurrentPlanName { get; set; } = "";

    /// <summary>Kupon uygulanmaya çalışıldıysa sonucu — UI durumları için.</summary>
    public string? CouponError { get; set; }

    public bool CouponApplied => Quote.DiscountAmount > 0;
}

/// <summary>Başarı ve hata sonuç sayfalarının ortak modeli.</summary>
public class CheckoutResultViewModel
{
    public string OrderReference { get; set; } = "";

    public string PlanName { get; set; } = "";

    public string BillingPeriodLabel { get; set; } = "";

    public decimal Total { get; set; }

    public string Currency { get; set; } = "TRY";

    public DateTime? CompletedAt { get; set; }

    /// <summary>Abonelik bitiş tarihi (başarı sayfasında gösterilir).</summary>
    public DateTime? AccessUntil { get; set; }

    /// <summary>Hata sayfasında kullanıcıya gösterilecek açıklama.</summary>
    public string? ErrorMessage { get; set; }

    public string? ErrorCode { get; set; }

    /// <summary>Hata sayfasındaki "Tekrar Dene" bağlantısı için.</summary>
    public int PlanId { get; set; }

    public string BillingPeriod { get; set; } = "";
}

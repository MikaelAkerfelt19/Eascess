using System.Text.RegularExpressions;

namespace Eascess_Domain.Constants;

/// <summary>
/// Fatura iletişim alanlarının (telefon, e-posta) doğrulama kuralları.
///
/// Kurallar SUNUCUDA burada tanımlanır ve tek doğruluk kaynağıdır. Ödeme
/// ekranındaki JavaScript aynı davranışı taklit eder, ancak istemci
/// doğrulaması yalnızca kullanıcı kolaylığıdır — karar buradadır.
/// </summary>
public static partial class BillingContactRules
{
    // ── Telefon ────────────────────────────────────────────────────

    /// <summary>
    /// Telefon numarasını yalnızca rakamlara indirger. Kullanıcı "+90 (555)
    /// 123 45 67" yazsa da karşılaştırma tek biçim üzerinden yapılır.
    /// </summary>
    public static string DigitsOnly(string? value) =>
        string.IsNullOrEmpty(value) ? "" : new string(value.Where(char.IsDigit).ToArray());

    /// <summary>
    /// Telefonu ülkeye göre doğrular ve "+<ülkekodu><ulusal numara>" biçiminde
    /// normalize eder.
    ///
    /// Kabul edilen girişler (Türkiye örneği, ülke kodu 90):
    ///   +90 555 123 45 67 · 90 555 123 45 67 · 0555 123 45 67 · 555 123 45 67
    /// Hepsi "+905551234567" olarak normalize edilir.
    ///
    /// Türkiye'de ulusal numara TAM 10 hane olmalıdır ve 0 ile başlayamaz.
    /// Diğer ülkelerde ülkeye özel uzunluk tanımlıysa o, değilse 6–14 hane aralığı uygulanır.
    /// </summary>
    public static PhoneValidationResult ValidatePhone(string? phone, string? countryCode)
    {
        var country = Countries.Find(countryCode) ?? Countries.Default;
        var digits = DigitsOnly(phone);

        if (digits.Length == 0)
            return PhoneValidationResult.Invalid("Telefon zorunludur.");

        // Ülke kodu baştaysa ayrıştır; yoksa numara zaten ulusal biçimdedir.
        var national = digits.StartsWith(country.DialCode, StringComparison.Ordinal)
            ? digits[country.DialCode.Length..]
            : digits;

        // Ulusal biçimde yazılan baştaki 0 (0555…) atılır.
        national = national.TrimStart('0');

        if (national.Length == 0)
            return PhoneValidationResult.Invalid("Geçerli bir telefon numarası girin.");

        if (country.PhoneDigits > 0)
        {
            if (national.Length != country.PhoneDigits)
            {
                return PhoneValidationResult.Invalid(
                    $"{country.Name} için telefon numarası +{country.DialCode} sonrası " +
                    $"{country.PhoneDigits} haneli olmalıdır.");
            }
        }
        else if (national.Length is < Countries.MinPhoneDigits or > Countries.MaxPhoneDigits)
        {
            return PhoneValidationResult.Invalid(
                $"Telefon numarası +{country.DialCode} sonrası " +
                $"{Countries.MinPhoneDigits}–{Countries.MaxPhoneDigits} hane olmalıdır.");
        }

        return PhoneValidationResult.Valid($"+{country.DialCode}{national}");
    }

    // ── E-posta ────────────────────────────────────────────────────

    /// <summary>
    /// E-posta biçim kontrolü. [EmailAddress] özniteliğinden daha sıkıdır:
    /// tam olarak bir "@" ister, alan adında en az bir nokta ve en az iki
    /// harfli bir uzantı arar. Böylece "ada@localhost" veya "a@b" gibi
    /// faturaya yazılamayacak adresler baştan elenir.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var trimmed = email.Trim();

        // Tek "@" şartı — "a@b@c.com" gibi girişler elenir.
        if (trimmed.Count(c => c == '@') != 1) return false;
        if (trimmed.Length > 256) return false;
        if (trimmed.Contains("..", StringComparison.Ordinal)) return false;

        return EmailPattern().IsMatch(trimmed);
    }

    /// <summary>E-postayı saklama biçimine getirir: kırpılmış ve küçük harfli.</summary>
    public static string NormalizeEmail(string? email) =>
        (email ?? "").Trim().ToLowerInvariant();

    /// <summary>
    /// Yerel kısım: harf/rakam ve . _ % + - ; alan adı: en az bir nokta ve
    /// 2+ harfli uzantı. Ayrıca yerel kısım ile alan adı nokta ile
    /// başlayıp bitemez.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9._%+-]*[A-Za-z0-9])?@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}$")]
    private static partial Regex EmailPattern();
}

public readonly record struct PhoneValidationResult(bool IsValid, string? Normalized, string? ErrorMessage)
{
    public static PhoneValidationResult Valid(string normalized) => new(true, normalized, null);
    public static PhoneValidationResult Invalid(string message) => new(false, null, message);
}

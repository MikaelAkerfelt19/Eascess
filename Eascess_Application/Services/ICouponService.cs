using Eascess_Domain.Entities;

namespace Eascess_Application.Services;

/// <summary>
/// Kupon/promosyon kodu doğrulama.
///
/// Doğrulama YALNIZCA sunucuda yapılır; istemci yalnızca kodu gönderir,
/// indirim tutarını asla göndermez.
/// </summary>
public interface ICouponService
{
    /// <summary>
    /// Kodu doğrular ve verilen ara toplam için indirim tutarını hesaplar.
    /// Geçersiz kodda IsValid=false döner; çağıran indirim uygulamaz.
    /// </summary>
    CouponValidationResult Validate(string? code, decimal subtotal, Plan plan, string billingPeriod);
}

public class CouponValidationResult
{
    public bool IsValid { get; init; }

    /// <summary>Normalize edilmiş kod (büyük harf, boşluksuz). Geçersizse null.</summary>
    public string? NormalizedCode { get; init; }

    public decimal DiscountAmount { get; init; }

    /// <summary>Kullanıcıya gösterilecek etiket, ör. "%25 indirim".</summary>
    public string? Label { get; init; }

    /// <summary>Geçersizse kullanıcıya gösterilecek sebep.</summary>
    public string? ErrorMessage { get; init; }

    public static CouponValidationResult None() => new() { IsValid = false };

    public static CouponValidationResult Invalid(string message) =>
        new() { IsValid = false, ErrorMessage = message };

    public static CouponValidationResult Success(string code, decimal amount, string label) =>
        new() { IsValid = true, NormalizedCode = code, DiscountAmount = amount, Label = label };
}

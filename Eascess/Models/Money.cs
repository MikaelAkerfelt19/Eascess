using System.Globalization;

namespace Eascess.Models;

/// <summary>
/// Para biçimlendirme. Uygulama genelinde kültür ayarlanmadığı için biçim
/// açıkça tr-TR ile yapılır — sunucunun bölgesel ayarı sonucu değiştirmez.
/// </summary>
public static class Money
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Ör. 1.234,56 ₺</summary>
    public static string Format(decimal amount, string currency = "TRY")
    {
        var number = amount.ToString("#,##0.00", Tr);
        return currency == "TRY" ? $"{number} ₺" : $"{number} {currency}";
    }
}

namespace Eascess.Models;

/// <summary>
/// Panel selamlaması. Dilimler:
///   00:00–05:59  İyi geceler
///   06:00–11:59  Günaydın
///   12:00–18:59  Merhabalar
///   19:00–23:59  İyi akşamlar
/// </summary>
public static class Greeting
{
    /// <summary>Verilen saate (0–23) karşılık gelen selamlama.</summary>
    public static string ForHour(int hour) => hour switch
    {
        >= 0 and < 6 => "İyi geceler",
        >= 6 and < 12 => "Günaydın",
        >= 12 and < 19 => "Merhabalar",
        _ => "İyi akşamlar",
    };

    /// <summary>Sunucunun yerel saatine göre selamlama.</summary>
    public static string Now() => ForHour(DateTime.Now.Hour);
}

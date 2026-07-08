namespace Eascess_Domain.Constants;

/// <summary>
/// Ödeme siparişi (PaymentOrder) yaşam döngüsü durumları.
///
/// Akış: Draft → Pending → (Paid | Failed | Canceled)
/// Draft   : sipariş oluşturuldu, sağlayıcıya henüz gidilmedi.
/// Pending : sağlayıcıya yönlendirildi, sonuç (callback) bekleniyor.
/// Paid    : ödeme doğrulandı, abonelik açıldı. TERMİNAL.
/// Failed  : sağlayıcı reddetti veya doğrulama başarısız. TERMİNAL.
/// Canceled: kullanıcı vazgeçti. TERMİNAL.
/// </summary>
public static class PaymentOrderStatus
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Canceled = "Canceled";

    /// <summary>Terminal durumlar bir daha değiştirilemez — çift tahsilatı önler.</summary>
    public static bool IsTerminal(string? status) =>
        status is Paid or Failed or Canceled;
}

/// <summary>
/// Payment.PaymentStatus alanı için durum sabitleri.
/// </summary>
public static class PaymentStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

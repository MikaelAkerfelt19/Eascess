using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Eascess_Domain.Entities;

/// <summary>
/// Ödeme ekranında oluşturulan sipariş kaydı — sağlayıcıya gitmeden ÖNCE yazılır.
///
/// Neden ayrı bir tablo: Payment kaydı ancak başarılı tahsilat sonrası ve bir
/// UserSubscription'a bağlı olarak oluşur. Sipariş ise abonelik açılmadan önce
/// var olmalı; ayrıca fatura anlık görüntüsü (billing snapshot), kupon ve
/// idempotency anahtarı gibi Payment'ta bulunmayan alanları taşır.
///
/// Tutar alanlarının tamamı SUNUCUDA PlanId üzerinden hesaplanır; istemciden
/// gelen tutarlar hiçbir koşulda buraya yazılmaz.
/// </summary>
[Index(nameof(OrderReference), Name = "IX_PaymentOrders_OrderReference", IsUnique = true)]
[Index(nameof(IdempotencyKey), Name = "IX_PaymentOrders_IdempotencyKey", IsUnique = true)]
[Index(nameof(UserId), Name = "IX_PaymentOrders_UserId")]
public class PaymentOrder
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Dışarıya açık sipariş numarası. URL'lerde ve sağlayıcı çağrılarında Id yerine
    /// bu kullanılır — ardışık Id'lerin sayım/tahmin edilmesini engeller.
    /// </summary>
    [StringLength(40)]
    public string OrderReference { get; set; } = null!;

    /// <summary>
    /// Çift gönderim koruması. Aynı anahtarla gelen ikinci POST yeni sipariş
    /// oluşturmaz, mevcut siparişi döndürür. Benzersiz indeks ile zorlanır.
    /// </summary>
    [StringLength(64)]
    public string IdempotencyKey { get; set; } = null!;

    [StringLength(450)]
    public string UserId { get; set; } = null!;

    public int PlanId { get; set; }

    /// <summary>BillingPeriods.Monthly | BillingPeriods.Yearly</summary>
    [StringLength(20)]
    public string BillingPeriod { get; set; } = null!;

    [StringLength(10)]
    public string Currency { get; set; } = null!;

    // ── Sunucuda hesaplanan tutarlar (hepsi KDV hariç, TaxAmount hariç) ──

    /// <summary>Sipariş anındaki plan birim fiyatı (aylık, KDV hariç).</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>Ödenen ay sayısı — aylıkta 1, yıllıkta 10 (2 ay hediye).</summary>
    public int BilledMonths { get; set; }

    /// <summary>UnitPrice × BilledMonths.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Subtotal { get; set; }

    [StringLength(40)]
    public string? CouponCode { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    /// <summary>Sipariş anında geçerli KDV oranı — sonradan oran değişse de fatura sabit kalır.</summary>
    [Column(TypeName = "decimal(6, 4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TaxAmount { get; set; }

    /// <summary>Tahsil edilecek nihai tutar: (Subtotal − Discount) + TaxAmount.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalAmount { get; set; }

    // ── Fatura bilgileri anlık görüntüsü ──
    // Kullanıcı profili sonradan değişse de faturanın çekildiği andaki bilgi korunur.

    [StringLength(200)]
    public string BillingFullName { get; set; } = null!;

    [StringLength(256)]
    public string BillingEmail { get; set; } = null!;

    [StringLength(40)]
    public string BillingPhone { get; set; } = null!;

    [StringLength(100)]
    public string BillingCountry { get; set; } = null!;

    [StringLength(100)]
    public string BillingCity { get; set; } = null!;

    [StringLength(500)]
    public string BillingAddress { get; set; } = null!;

    /// <summary>Kurumsal fatura talep edildi mi — true ise şirket alanları doldurulur.</summary>
    public bool IsCompany { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    public string? TaxOffice { get; set; }

    [StringLength(50)]
    public string? TaxNumber { get; set; }

    // ── Durum & sağlayıcı ──

    /// <summary>PaymentOrderStatus sabitlerinden biri.</summary>
    [StringLength(30)]
    public string Status { get; set; } = null!;

    /// <summary>Siparişi işleyen sağlayıcının adı (IPaymentProvider.ProviderName).</summary>
    [StringLength(50)]
    public string? PaymentProvider { get; set; }

    /// <summary>Sağlayıcının işlem numarası — mutabakat için saklanır.</summary>
    [StringLength(255)]
    public string? ProviderTransactionId { get; set; }

    [StringLength(50)]
    public string? ErrorCode { get; set; }

    /// <summary>Kullanıcıya gösterilebilir hata açıklaması. Hassas veri içermez.</summary>
    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    /// <summary>Ödeme başarılıysa oluşturulan Payment kaydı.</summary>
    public int? PaymentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(PlanId))]
    public virtual Plan Plan { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual AppUser User { get; set; } = null!;
}

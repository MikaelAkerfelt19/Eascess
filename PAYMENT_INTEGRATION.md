# Ödeme Sağlayıcı Entegrasyonu

Ödeme akışının tamamı hazırdır ve bugün uçtan uca çalışır — **gerçek sağlayıcı
çağrısı hariç**. Bu belge o çağrının nereye yazılacağını, hangi ayarların
gerektiğini ve sağlayıcılar arasında nasıl geçiş yapılacağını anlatır.

---

## 1. Kodu nereye yapıştıracaksınız

Tek bir dosya: **`Eascess_Infrastructure/Services/Payments/LivePaymentProvider.cs`**

İçinde **iki** entegrasyon noktası vardır; ikisi de tam olarak şu blokla işaretlidir:

```csharp
// ===== PAYMENT API INTEGRATION POINT =====
// Provider: <fill in>
// Required: API key, secret key, base URL
// Paste the provider SDK/HTTP call here. Expected return: PaymentResult
// =========================================
```

| Metot | Ne yapmalı | Beklenen dönüş |
|---|---|---|
| `CreatePaymentAsync(PaymentRequest)` | Sağlayıcıda ödeme oturumu açar | `PaymentResult.Redirect(url, …)` (tercih edilen), `PaymentResult.Html(html, …)` (3DS formu), `PaymentResult.Success(…)` veya `PaymentResult.Failure(…)` |
| `VerifyCallbackAsync(PaymentCallbackContext)` | **Önce imzayı doğrular**, sonra sonucu çevirir | `PaymentResult.Success(…)`, `PaymentResult.Failure(…)` veya imza tutmuyorsa `PaymentResult.InvalidSignature(…)` |

Her iki metot da bugün `NotImplementedException` fırlatır. Kod yapıştırıldığında
`throw` satırını silin.

### Elinizdeki veriler

`PaymentRequest` içindeki her alan **sunucuda** üretilmiştir; hiçbiri istemciden
gelmez:

```
Amount          tahsil edilecek nihai tutar (KDV dahil)
NetAmount       KDV hariç net tutar (indirim düşülmüş)
TaxAmount       KDV tutarı
Currency        "TRY"
OrderReference  sipariş numarası — callback'te geri bekleriz
IdempotencyKey  sağlayıcı destekliyorsa idempotency başlığına koyun
Buyer           ad, e-posta, telefon, (varsa) şirket/vergi bilgisi
BillingAddress  ülke, şehir, adres
BasketItems     kalem kırılımı (toplamı NetAmount'a eşit)
CallbackUrl     sağlayıcının geri döneceği mutlak URL
BuyerIpAddress  risk analizi isteyen sağlayıcılar için
```

### Uyulması zorunlu kurallar

- **Kart verisi bu koda girmez.** Akış hosted/redirect kurgulanmıştır; kendi
  formumuzda kart alanı yoktur ve eklenmemelidir.
- `VerifyCallbackAsync` **imzayı doğrulamadan** sonuç üretmez. Doğrulama
  `callback.RawBody` üzerinden yapılır — gövdeyi deserialize edip yeniden
  serialize etmeyin, imza bozulur. Karşılaştırmayı
  `CryptographicOperations.FixedTimeEquals` ile sabit sürede yapın.
- `PaymentResult.OrderReference` **mutlaka** doldurulmalıdır; sipariş bununla bulunur.
- `PaymentResult.PaidAmount` doldurulursa `CheckoutService` tutarı siparişle
  karşılaştırır ve **eksik tahsilatta aboneliği açmaz**. Doldurmanız önerilir.
- API anahtarı, gizli anahtar veya sağlayıcının ham yanıtı **loglanmaz** ve
  `ErrorMessage` içine konmaz.

---

## 2. Ayarlanacak yapılandırma anahtarları

`appsettings.json` içindeki `Payments` bölümü **boş değerlerle** bulunur —
gizli anahtarlar repoya girmez:

```json
"Payments": {
  "Provider": "Sandbox",
  "ApiKey": "",
  "SecretKey": "",
  "BaseUrl": "",
  "CallbackUrl": "",
  "TimeoutSeconds": 20,
  "SandboxAutoApprove": false
}
```

| Anahtar | Açıklama |
|---|---|
| `Provider` | `Sandbox` = sahte sağlayıcı (gerçek tahsilat yok). Başka herhangi bir değer → `LivePaymentProvider`. |
| `ApiKey` | Sağlayıcı API anahtarı. **Zorunlu (Live).** |
| `SecretKey` | Gizli anahtar; callback imzası bununla doğrulanır. **Zorunlu (Live).** |
| `BaseUrl` | Sağlayıcı API kök adresi. **Zorunlu (Live).** |
| `CallbackUrl` | Sağlayıcının geri döneceği mutlak URL. Boşsa isteğin host'undan üretilir. **Üretimde açıkça ayarlayın** — ters vekil arkasında host yanlış çözülebilir. |
| `TimeoutSeconds` | Sağlayıcı çağrılarında zaman aşımı. |
| `SandboxAutoApprove` | Yalnızca Sandbox: onay ekranını atlayıp doğrudan başarı üretir (otomatik testler için). |

### Değerleri nasıl vereceksiniz

**Asla `appsettings.json`'a yazmayın.**

Geliştirme (user-secrets — `UserSecretsId` zaten tanımlı):

```bash
dotnet user-secrets set "Payments:Provider"   "Live"    --project Eascess
dotnet user-secrets set "Payments:ApiKey"     "..."     --project Eascess
dotnet user-secrets set "Payments:SecretKey"  "..."     --project Eascess
dotnet user-secrets set "Payments:BaseUrl"    "https://sandbox-api.saglayici.com" --project Eascess
```

Üretim (ortam değişkeni / Azure App Settings — çift alt çizgi bölüm ayırıcıdır):

```
Payments__Provider    = Live
Payments__ApiKey      = ...
Payments__SecretKey   = ...
Payments__BaseUrl     = https://api.saglayici.com
Payments__CallbackUrl = https://app.eascess.io/Checkout/Callback
```

---

## 3. Callback URL

```
POST (veya GET)  https://<host>/Checkout/Callback
```

- `CheckoutController.Callback` karşılar. **Anonimdir** ve antiforgery jetonu
  aranmaz — istek dış bir sistemden gelir. Güvenlik tamamen **imza
  doğrulamasına** dayanır.
- Sağlayıcı panelinde bu adresi tanımlayın ve `Payments:CallbackUrl` ile aynı
  olduğundan emin olun.
- Aynı bildirimin birden çok kez gelmesi güvenlidir: sipariş `Paid` durumundaysa
  yeniden işlenmez, ikinci bir abonelik veya fatura oluşmaz.

---

## 4. Sağlayıcı değiştirme

`Program.cs` içinde tek yer:

```csharp
builder.Services.AddScoped<IPaymentProvider>(sp =>
{
    var provider = builder.Configuration.GetValue("Payments:Provider", "Sandbox");

    return string.Equals(provider, "Sandbox", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<SandboxPaymentProvider>(sp)
        : sp.GetRequiredService<LivePaymentProvider>();
});
```

Pratikte **kod değişikliği bile gerekmez**: `Payments:Provider` değerini
`Sandbox` → `Live` yapmak yeterlidir.

Birden fazla gerçek sağlayıcı gerekirse `IPaymentProvider`'ı uygulayan yeni bir
sınıf yazıp bu `switch`'e bir dal ekleyin. `ProviderName` özelliği yapılandırmadaki
değerle eşleşmelidir.

---

## 5. Bugün nasıl test edilir

`Provider = "Sandbox"` (varsayılan) ile akışın tamamı çalışır:

1. `/Subscription` → bir planda **"Bu Plana Geç"**, ya da `/Home/Pricing` → **"Pro'ya Geç" / "Ultra'ya Geç"**.
2. Ödeme ekranı: fatura bilgileri, ülke/il seçimi, kurumsal fatura anahtarı, kupon, dönem seçimi.
   Test kuponları: `EASCESS10`, `WCAG25`, `YILLIK15` (yalnızca yıllık), `ULTRA20` (yalnızca Ultra).
   Ülkeyi değiştirince KDV satırının ve toplamın güncellendiğini görmelisiniz.
3. **Öde** → sağlayıcının ödeme onay ekranı (`/Checkout/Sandbox`).
4. **Ödemeyi Onayla** → başarı sayfası, abonelik açılır, `Payment` ve `Invoice`
   kayıtları oluşur, varsa Ücretsiz abonelik kapanır ve plan tanıtım penceresi
   bir kez gösterilir. **Vazgeç** → hata sayfası, abonelik açılmaz.

Sandbox sağlayıcı da callback'i **gerçekten imzalar ve doğrular** (HMAC-SHA256).
Gizli alanları değiştirirseniz callback reddedilir ve sipariş güncellenmez —
imza doğrulama yolu bugünden test edilebilir durumdadır.

> **Kullanıcıya dönük metin kuralı:** `/Checkout/Sandbox` ekranı gerçek bir ödeme
> sayfasının dilini konuşur; "test", "sahte", "gerçek tahsilat yapılmaz" gibi
> ifadeler bilerek yoktur. Sandbox'ta olduğunuzu yapılandırmadan
> (`Payments:Provider`) anlayın, ekrandan değil.

---

## 6. Akışın haritası

```
/Checkout?planId=&period=          fatura formu + sunucuda hesaplanan özet
  └─ POST /Checkout/Quote          kupon / dönem / ÜLKE değişiminde özeti yeniden hesaplar
  └─ POST /Checkout/Start          siparişi oluşturur → IPaymentProvider.CreatePaymentAsync
       ├─ RedirectRequired         sağlayıcıya yönlendirir (URL veya HTML)
       ├─ Succeeded                siparişi tamamlar
       └─ Failed                   siparişi başarısız kapatır
  ANY  /Checkout/Callback          IPaymentProvider.VerifyCallbackAsync → imza doğrulanır
  GET  /Checkout/Success?orderRef= başarı sayfası
  GET  /Checkout/Failure?orderRef= hata sayfası
```

### Doğruluk garantileri (yeri gelince bozmayın)

- **Fiyat sunucuda hesaplanır.** İstemciden yalnızca `PlanId`, dönem, ülke kodu
  ve kupon *kodu* gelir. `CheckoutService.BuildQuote` tek hesap noktasıdır;
  formda tutar alanı yoktur.
- **KDV fatura ülkesine göredir.** Oran `Eascess_Domain/Constants/Countries.cs`
  içindeki ülke kaydından okunur (Türkiye %20, Almanya %19, Macaristan %27,
  ABD %0 …) ve sipariş anındaki oran `PaymentOrder.TaxRate` alanına yazılır —
  oran sonradan değişse de kesilmiş fatura sabit kalır. KDV her zaman
  **indirim düşüldükten sonraki** tutar üzerinden hesaplanır.
  **Kapsam dışı (gerekirse ayrıca ele alın):** AB içi B2B *reverse charge*
  (geçerli VAT numarasıyla %0) ve ABD gibi eyalet bazlı satış vergisi olan
  ülkeler. Oranlar 2024 standart oranlarıdır; mali mevzuat değişince
  `Countries.cs` güncellenmelidir.
- **İletişim alanları normalize edilir.** Telefon `+<ülkekodu><ulusal numara>`
  biçimine çevrilir (Türkiye'de ulusal kısım tam 10 hane), e-posta küçük harfe
  indirgenir. Kurallar `Eascess_Domain/Constants/BillingContactRules.cs`
  içindedir ve tek doğruluk kaynağıdır; `checkout.js` yalnızca aynı kuralı
  kullanıcı kolaylığı için taklit eder.
- **Türkiye'de il, 81 il listesinden seçilir** (`TurkeyProvinces.cs`); diğer
  ülkelerde şehir serbest metindir. Doğrulama hem form modelinde hem
  `CheckoutService` içinde yapılır — controller atlansa bile yanlış veri geçmez.
- **Çift tahsilat koruması.** `PaymentOrder.IdempotencyKey` = SHA-256(kullanıcı +
  form jetonu), benzersiz indeksli. Aynı formun ikinci gönderimi yeni sipariş
  açmaz; sipariş ödenmişse kullanıcı doğrudan başarı sayfasına gider.
- **Terminal durumlar.** `Paid`/`Failed`/`Canceled` bir daha değişmez.
- **Tutar eşleşmesi.** Sağlayıcı bir tutar bildirirse sipariş tutarıyla
  karşılaştırılır; eşleşmezse abonelik açılmaz.
- **Log hijyeni.** Sipariş numarası, tutar, plan ve sağlayıcı adı loglanır.
  Kart verisi (hiç yoktur), anahtarlar ve sağlayıcı ham yanıtı loglanmaz.
  `Payment.RawResponse` sağlayıcının yanıtını değil, bizim ürettiğimiz
  sadeleştirilmiş özeti tutar.

---

## 7. Devreye alma kontrol listesi

- [ ] `LivePaymentProvider.CreatePaymentAsync` entegrasyon noktası dolduruldu, `throw` silindi
- [ ] `LivePaymentProvider.VerifyCallbackAsync` entegrasyon noktası dolduruldu, `throw` silindi
- [ ] İmza doğrulaması `RawBody` üzerinden ve sabit sürede yapılıyor
- [ ] `PaymentResult.OrderReference` her dönüşte dolduruluyor
- [ ] `ProviderName` yapılandırmadaki `Payments:Provider` değeriyle eşleşiyor
- [ ] `Payments__ApiKey` / `SecretKey` / `BaseUrl` ortam değişkeni olarak tanımlandı
- [ ] `Payments__CallbackUrl` üretim adresiyle ayarlandı ve sağlayıcı panelinde tanımlı
- [ ] `Payments__Provider` = `Live`
- [ ] Sağlayıcının sandbox ortamında başarı **ve** hata senaryosu denendi
- [ ] Tekrarlanan callback denendi (ikinci abonelik/fatura oluşmuyor)

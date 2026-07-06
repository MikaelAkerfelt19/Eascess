# Plan Vaatleri Uygulama Test Raporu

Fiyatlandırma sayfasındaki plan vaatlerinin kodda gerçekten uygulandığını doğrulamak için
20 farklı kullanıcı, farklı plan ve abonelik varyasyonlarıyla sisteme tanımlandı ve tüm
vaat senaryoları uçtan uca test edildi. Bu rapor versiyonludur: her çalıştırma, bulunan
hatalar ve yapılan düzeltmeler ayrı sürüm başlıkları altında tutulur.

- **Test dosyası:** `Eascess.Tests/Integration/PlanEnforcementIntegrationTests.cs`
- **Düzenek:** Gerçek HTTP pipeline (WebApplicationFactory) + InMemory DB.
  Gemini yerine fake alt metin üretici, SMTP yerine alıcıları kaydeden fake e-posta servisi.
- **Çalıştırma komutu:** `dotnet test Eascess.Tests/Eascess.Tests.csproj --filter "FullyQualifiedName~PlanEnforcement"`

---

## Test Düzeneği: 20 Kullanıcı Matrisi

| # | Kullanıcı | Varyasyon | Beklenen plan |
|---|-----------|-----------|---------------|
| 1 | `u01-free-nosub` | Hiç aboneliği yok | Ücretsiz |
| 2 | `u02-free-expired-pro` | Pro aboneliği 10 gün önce bitti | Ücretsiz |
| 3 | `u03-free-inactive-pro` | Pro tarih aralığında ama `IsActive=false` | Ücretsiz |
| 4 | `u04-free-deleted-ultra` | Ultra aboneliği soft-delete edilmiş | Ücretsiz |
| 5 | `u05-free-future-pro` | Pro 10 gün **sonra** başlayacak | Ücretsiz |
| 6 | `u06-pro-active` | Standart aktif Pro | Pro |
| 7 | `u07-pro-trial` | 14 günlük deneme (kayıt akışıyla birebir aynı kurgu) | Pro |
| 8 | `u08-pro-plus-free` | Pro + Ücretsiz aynı anda aktif | Pro |
| 9 | `u09-pro-expiring-soon` | Pro 1 saat sonra bitiyor (hâlâ geçerli) | Pro |
| 10 | `u10-pro-domain-limit` | Pro, 3 domain ile limitte | Pro |
| 11 | `u11-ultra-active` | Standart aktif Ultra | Ultra |
| 12 | `u12-ultra-plus-pro` | Ultra + Pro aktif → üst kademe kazanmalı | Ultra |
| 13 | `u13-ultra-fresh` | Ultra bugün başladı | Ultra |
| 14 | `u14-ultra-yearly` | Yıllık Ultra (bitiş +1 yıl) | Ultra |
| 15 | `u15-ultra-downgrade-plan` | Ultra aktif + gelecekte Ücretsiz satırı | Ultra |
| 16 | `u16-ent-active` | Standart Kurumsal | Kurumsal |
| 17 | `u17-ent-plus-pro` | Kurumsal (fiyat=0) + Pro (600₺) → kademe fiyatı yenmeli | Kurumsal |
| 18 | `u18-ent-longterm` | 10 yıllık Kurumsal | Kurumsal |
| 19 | `u19-free-trial-ended` | Deneme dün bitti → Ücretsiz'e düştü | Ücretsiz |
| 20 | `u20-free-downgraded` | Pro'dan düşmüş; Pro dönemindeyken widget'ını özelleştirmişti | Ücretsiz |

### Test Edilen Vaatler

1. **Plan çözünürlüğü** — 20 kullanıcının her biri doğru plana çözümleniyor mu (20 senaryo)
2. **Özellik kapıları** — çözümlenen plan, vaat matrisiyle uyumlu mu (7 senaryo)
3. **AI kotası** — Ücretsiz kullanıcılar `/api/scan/alt-text`'te reddediliyor, ücretliler geçiyor mu (5 senaryo)
4. **Domain limiti** — limitteki kullanıcı yeni domain ekleyemiyor mu (3 senaryo)
5. **E-posta bildirimleri** — aylık rapor e-postası yalnızca Ultra/Kurumsal'a mı gidiyor (1 senaryo)
6. **Widget özelleştirme** — plan dışına düşen kullanıcının widget'ı varsayılana dönüyor mu (2 senaryo)

---

## v1 — İlk Çalıştırma (2026-07-06)

**Sonuç: 38 testten 37 başarılı, 1 BAŞARISIZ — gerçek bir plan ihlali yakalandı.**

```
Başarısız Eascess.Tests.Integration.PlanEnforcementIntegrationTests
          .WidgetConfig_UcretsizeDusenKullanici_VarsayilanaDoner [11 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
Expected: "#0056b3"
Actual:   "#ff0000"

Başarısız! - Başarısız: 1, Başarılı: 37, Atlanan: 0, Toplam: 38
```

### BULGU-1: İndirgenen kullanıcının widget özelleştirmesi çalışmaya devam ediyor

- **Senaryo:** `u20-free-downgraded` — Pro dönemindeyken widget temasını `#ff0000`,
  konumunu `top-left` yapmış ve logo yüklemiş; Pro aboneliği 3 gün önce bitmiş.
- **Beklenen:** Widget özelleştirme "Pro ve üzeri" vaadi olduğundan, plan Ücretsiz'e
  düşünce `/api/widget/config` varsayılan görünümü (`#0056b3`, `bottom-right`, logosuz)
  dönmeli.
- **Gerçekleşen:** Endpoint kayıtlı özelleştirmeyi (`#ff0000`, `top-left`, logo) servis
  etmeye devam etti. Yani özelleştirme **düzenleme ekranında** engellenmişti ama
  **teslim noktasında** (müşteri sitesindeki widget) engellenmemişti.
- **Kök neden:** `WidgetService.GetConfigByLicenseKeyAsync` domain sahibinin planına
  hiç bakmadan `WidgetSetting` kaydını aynen dönüyordu.

### v1'de doğru çalıştığı doğrulanan davranışlar

- 20 kullanıcının tamamı doğru plana çözümlendi — süresi dolmuş, pasif, silinmiş ve
  gelecekte başlayan abonelikler doğru şekilde elendi; Kurumsal+Pro çakışmasında
  kademe (TierRank) fiyatı yendi.
- Ücretsiz kullanıcılar (`u01`, `u19`) AI taramasında `429 QUOTA_EXCEEDED` aldı;
  Pro/Ultra/Kurumsal kullanıcılar `200 OK` aldı.
- Aylık rapor e-postası yalnızca `u11` (Ultra) ve `u16` (Kurumsal) adreslerine gitti;
  `u06` (Pro) ve `u20` (Ücretsiz) almadı.
- Domain limitleri: `u01` 1/1, `u10` 3/3 ile limitte (ekleme kuralı engelliyor);
  `u11` 1/10 ile ekleme serbest.

---

## Düzeltme (v1 → v2)

**Değişen dosya:** `Eascess_Application/Services/WidgetService.cs`

`GetConfigByLicenseKeyAsync` artık domain sahibinin aktif planını `IPlanService`
üzerinden çözümlüyor. Plan `HasWidgetCustomization` içermiyorsa (Ücretsiz), kayıtlı
özelleştirme silinmeden korunuyor ama config **varsayılan görünümle** dönülüyor:
tema `#0056b3`, konum `bottom-right`, dil `tr`, logo ve başlık boş, "powered by"
görünür. Kullanıcı tekrar Pro'ya yükselirse eski özelleştirmesi kendiliğinden geri
gelir. `IsAiEnabled` tercihi korunur — AI erişimi zaten kota (Ücretsiz=0) ile ayrıca
kısıtlanır.

---

## v2 — Düzeltme Sonrası Çalıştırma (2026-07-06)

**Sonuç: 38/38 başarılı.** Aynı matris, `u20-free-downgraded` senaryosu dahil tamamı geçti.

```
Başarılı!  - Başarısız: 0, Başarılı: 38, Atlanan: 0, Toplam: 38, Süre: 3 s
```

Regresyon kontrolü için tüm paket de çalıştırıldı:

```
Başarılı!  - Başarısız: 0, Başarılı: 104, Atlanan: 0, Toplam: 104, Süre: 2 s
```

(104 = önceki 66 test + bu matristeki 38 test. Aktif Pro kullanıcısının
özelleştirmesinin korunduğu da ayrı senaryoyla doğrulandı: `u06` teması `#22aa55`
olarak servis edilmeye devam ediyor.)

---

## Açık Sorular — Verilen Ürün Kararları (2026-07-06)

v2'de "açık soru" olarak raporlanan 4 konunun tamamı karara bağlandı ve v3'te uygulandı:

| # | Soru | Karar | Durum |
|---|------|-------|-------|
| 1 | Limit üstü mevcut domainler ne olacak? | **Ücretsiz'e düşen kullanıcının TÜM domainleri silinir; yeniden bağlaması gerekir.** | v3'te uygulandı |
| 2 | Yüklü logo dosyaları ne olacak? | **60 gün bekletilir.** Kullanıcı bu sürede ücretli plana dönerse logolar kalır; dönmezse dosya ve kayıt kalıcı silinir. | v3'te uygulandı |
| 3 | Deneme bitişi anı? | **Gün bazlı: deneme 14. günün sonunda gece 00:00 UTC'de biter.** Aynı gün kayıt olan herkesin denemesi aynı anda sona erer. | v3'te uygulandı |
| 4 | İleride yeni plan eklenirse? | **Yeni plan eklenmeyecek.** Yine de eklenirse diye `PlanIds.cs` başına, güncellenmesi gereken 5 noktayı sayan kalıcı bir not bırakıldı; başka bir değişiklik yapılmadı. | v3'te not bırakıldı |

---

## v3 — Ürün Kararlarının Uygulanması ve Yeni Varyasyonlar (2026-07-06)

### Yapılan değişiklikler

1. **`DowngradeCleanupService` + `DowngradeCleanupJob` (00:05 UTC):** Eski
   `TrialExpiryJob`'ın yerini aldı ve kapsamı genişletti. Her gece:
   - Süresi dolan **tüm** ücretli abonelikler (yalnızca Pro denemesi değil) pasifleştirilir,
     bekleyen Ücretsiz plan aktive edilir.
   - Ücretsiz'e düşen kullanıcının **tüm domainleri soft-delete edilir**, widget ayarları
     pasifleştirilir (karar #1). Kullanıcının başka geçerli ücretli planı varsa
     (ör. Ultra aktifken Pro bitti) domainlere dokunulmaz.
   - Son ücretli abonelik bitişinin üzerinden **60 gün** geçen kullanıcıların logo
     dosyaları diskten ve kayıttan kalıcı silinir (karar #2). Ücretli plana dönen
     kullanıcı atlanır — logoları korunur.
2. **`TrialPolicy` (Domain katmanı):** Deneme bitişi tek merkezde:
   `TrialEndUtc(kayıtAnı) = kayıtGünü + 14 gün, saat 00:00 UTC` (karar #3).
   Kayıt akışı (`AccountController.Register`) `TrialEndsAt`, Pro deneme aboneliği
   bitişi ve Ücretsiz planın başlangıcını bu değerden alıyor.
3. **Yeni plan notu:** `PlanIds.cs` doküman yorumuna, olası bir plan ekleme durumunda
   birlikte güncellenmesi gereken 5 nokta (sabitler, TierRank/özellik kapıları,
   fiyatlandırma sayfası, migration+seed, test matrisleri) yazıldı (karar #4).

### Yeni test varyasyonları (`DowngradeCleanupTests` + `TrialPolicyTests`)

| Senaryo | Kurgu | Beklenen | Sonuç |
|---------|-------|----------|-------|
| `v01-pro-expired` | Pro dün bitti, 2 domain | Tüm domainler silinir, widget ayarları pasifleşir, Pro pasif/Ücretsiz aktif | ✓ |
| `v02-ultra-keeps` | Ultra aktif + Pro dün bitti | Plan Ultra kalır, domainler **kalır**, logosu işlenmez | ✓ |
| `v03-logo-purge` | Ücretli abonelik 61 gün önce bitti, logo dolu | Logo kalıcı silinir | ✓ |
| `v04-logo-waiting` | Ücretli abonelik 10 gün önce bitti, logo dolu | Bekleme sürüyor — logo kalır | ✓ |
| `v05-returned` | Eski abonelik 61 gün önce bitti AMA kullanıcı Pro'ya geri döndü | Logo korunur | ✓ |
| Trial politikası | Kayıt 06.07 15:42 | Bitiş 20.07 **00:00** (tam gece yarısı) | ✓ |
| Trial politikası | Aynı gün sabah/gece kayıt | İkisinin de bitişi aynı an | ✓ |

### v3 çalıştırma çıktısı

```
Başarılı!  - Başarısız: 0, Başarılı: 113, Atlanan: 0, Toplam: 113, Süre: 2 s
```

(113 = v2'deki 104 test + 7 downgrade temizlik senaryosu + 2 deneme politikası testi.
v1–v2'deki 38'lik plan matrisi değişmeden geçmeye devam ediyor.)

### v3 notları

- v2'deki BULGU-1 düzeltmesi (config teslim noktasında plan kontrolü) v3'te de gerekli
  kalıyor: deneme gece 00:00'da biter, temizlik 00:05'te çalışır — aradaki 5 dakikalık
  pencerede ve job'ın herhangi bir sebeple gecikmesi durumunda widget yine varsayılana
  döner. İki katman birbirini tamamlar.
- Plan matrisindeki `u19`/`u20` kullanıcıları "düşmüş ama temizlik henüz çalışmamış"
  ara durumu temsil eder; üretimde bu durum en fazla bir gece sürer.

---

## Sürüm Geçmişi

| Sürüm | Tarih | Sonuç | Not |
|-------|-------|-------|-----|
| v1 | 2026-07-06 | 37/38 | BULGU-1: indirgenen kullanıcıda widget özelleştirmesi aktif kalıyordu |
| v2 | 2026-07-06 | 38/38 (tam paket 104/104) | BULGU-1 düzeltildi: config teslim noktasında plan kontrolü |
| v3 | 2026-07-06 | 113/113 | 4 ürün kararı uygulandı: downgrade'de domain silme, 60 günlük logo bekleme, gece 00:00 deneme bitişi, yeni plan notu |
| v4 | 2026-07-06 | 132/132 | Son güvenlik denetimi: SSRF açığı (BULGU-2) kapatıldı, hata mesajı sızıntıları giderildi |

---

## v4 — Son Güvenlik Denetimi (2026-07-06)

Bütün sistem, özellikle **bilgi sızıntısı** ve **sistem çökmesine/iç ağ erişimine**
yol açabilecek açıklara odaklanarak son kez elden geçirildi. Denetlenen yüzeyler:
tüm controller'lar, iki middleware (CORS + API hata), giden HTTP istemcileri
(WCAG tarayıcı, AI görsel indirme, Gemini), dosya yükleme/silme yolları, kimlik
doğrulama, rate limiting ve abonelik/plan kapıları.

### BULGU-2 (Kritik) — SSRF: `POST /api/scan/alt-text` üzerinden iç ağ erişimi

**Açık:** AI alt-metin ucu yalnızca lisans anahtarıyla korunuyor; lisans anahtarı
ise müşteri sitelerindeki widget `<script>` etiketinde herkese açık. Bu uç,
istemcinin verdiği görsel URL'lerini `GeminiAltTextGeneratorService` içinde
sunucu tarafında **hiçbir IP filtresi olmadan ve yönlendirme açıkken** indiriyordu.
Saldırgan `http://169.254.169.254/…` (bulut metadata → kimlik bilgisi hırsızlığı)
veya iç ağ adreslerine istek attırabilir; herkese açık tarama (`TestSite`) için
konan koruma bu yolda ve kimlik doğrulamalı WCAG tarama yolunda yoktu. Ayrıca
mevcut ön-kontrol bile bir HTTP yönlendirmesiyle (redirect → iç IP) atlatılabilirdi.

**Kök neden:** Koruma yalnızca isteğin ilk URL'sini kontrol eden bir "önce doğrula,
sonra bağlan" tasarımıydı; DNS-rebinding ve yönlendirmeleri kapsamıyordu.

**Düzeltme:** Koruma bağlantı katmanına indirildi. Yeni
`Eascess_Application/Security/PrivateNetworkGuard.cs`, `SocketsHttpHandler.ConnectCallback`
olarak devreye girer ve **her TCP bağlantısında** (ilk istek + tüm yönlendirmeler)
hedef IP'yi çözüp doğrular; özel/rezerve/loopback/CGNAT/metadata adreslerine
bağlanmayı reddeder. Bağlanılan IP, doğrulanan IP'nin ta kendisi olduğu için
TOCTOU/DNS-rebinding açığı oluşmaz. Tüm giden istemciler bu geçitten geçirildi:
- `WcagScanner` (herkese açık + kimlik doğrulamalı tarama) → `SocketsHttpHandler` + ConnectCallback.
- Yeni `AltTextImageDownloader` (AI görsel indirme) → ConnectCallback **+ yönlendirme kapalı**
  + yalnızca `http/https` şema kabulü (`file://`, `gopher://` reddedilir).

### İkincil düzeltmeler

- **Bilgi sızıntısı — hata mesajları:** `PublicScanService` ve `WcagScanService`,
  yakalanan istisnanın `ex.Message` değerini doğrudan kullanıcıya döndürüyordu
  (iç ağ adı/altyapı detayı sızdırabilir). Artık ayrıntı yalnızca loglanır;
  kullanıcıya genel bir mesaj döner.
- **Tek kaynak:** `HomeController` içindeki kopya IP-kontrol mantığı kaldırılıp
  paylaşılan `PrivateNetworkGuard`'a bağlandı; kapsam da genişledi (127/8 tamamı,
  multicast/rezerve 224+, 192.0.0.0/24).

### Denetlenip **temiz** bulunanlar (düzeltme gerekmedi)

- **Yetkilendirme/IDOR:** Tüm MVC controller'lar `[Authorize]`; kaynak sorguları
  `userId` ile filtreleniyor (`Domain`, `Report`, `WidgetSetting`). Sahiplik kontrolü
  `Delete`/`Script`/`Analytics`/`Detail` uçlarında mevcut.
- **Dosya yükleme:** MIME beyaz listesi + boyut limiti + uzantının **MIME'den**
  türetilmesi (stored-XSS'e karşı `.html` gibi uzantılar engelli). Logo silmede
  path-traversal guard'ı (`logosRoot` prefix kontrolü) hem controller'da hem
  temizlik servisinde mevcut.
- **Kimlik:** Brute-force kilidi (5 deneme/5 dk), açık redirect'e karşı
  `Url.IsLocalUrl` kontrolü, tüm POST'larda `[ValidateAntiForgeryToken]`.
- **Rate limiting:** IP bazlı partitioned policy'ler (`ai-scan` 10/dk, `public-api` 60/dk).
- **CORS:** `DynamicCorsMiddleware` yalnızca kayıtlı domainlere izin, `Vary: Origin`
  ile cache zehirlenmesine karşı korumalı.
- **Güvenlik başlıkları:** HSTS, X-Frame-Options, X-Content-Type-Options, CSP,
  Referrer-Policy, Permissions-Policy `Program.cs`'te ekli.

### v4 çalıştırma çıktısı

```
Başarılı!  - Başarısız: 0, Başarılı: 132, Atlanan: 0, Toplam: 132, Süre: 3 s
```

(132 = v3'teki 113 test + `PrivateNetworkGuardTests`'ten 19 SSRF senaryosu —
metadata/loopback/özel ağ/CGNAT/multicast/IPv6 blok + genel adres geçiş doğrulaması.)

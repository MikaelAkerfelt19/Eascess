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

## Açık Sorular / Ürün Kararı Bekleyen Konular

Bu maddeler hata değil; testler sırasında ortaya çıkan ve ürün kararı gerektiren durumlar:

1. **Limit üstü mevcut domainler:** Pro'dan (3 domain) Ücretsiz'e (1 domain) düşen bir
   kullanıcının mevcut 3 domain'i silinmiyor; yalnızca **yeni** ekleme engelleniyor.
   Fazla domainlerin widget'ları çalışmaya devam ediyor. İstenirse downgrade'de en eski
   domain hariç diğerleri pasifleştirilebilir.
2. **Yüklü logo dosyaları:** İndirgenen kullanıcının logosu artık servis edilmiyor ama
   dosya diskte ve kayıt DB'de duruyor (yeniden yükseltmede geri gelsin diye bilinçli
   tercih). Kalıcı silme istenirse ayrıca uygulanmalı.
3. **Deneme bitişi anı:** `u09-pro-expiring-soon` bitişten 1 saat önce hâlâ Pro
   sayılıyor (`EndDate >= now` kuralı) — bitiş günü *dahil* davranışı bilinçli mi,
   onaylanmalı.
4. **Kurumsal fiyatı 0:** Kademe sıralaması artık `TierRank` ile yapılıyor; ileride
   yeni plan eklenirse `PlanIds` + `TierRank` + fiyatlandırma sayfası birlikte
   güncellenmeli (test matrisi uyumsuzluğu yakalar).

---

## Sürüm Geçmişi

| Sürüm | Tarih | Sonuç | Not |
|-------|-------|-------|-----|
| v1 | 2026-07-06 | 37/38 | BULGU-1: indirgenen kullanıcıda widget özelleştirmesi aktif kalıyordu |
| v2 | 2026-07-06 | 38/38 (tam paket 104/104) | BULGU-1 düzeltildi: config teslim noktasında plan kontrolü |

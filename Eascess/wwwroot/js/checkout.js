/* ================================================================
   Eascess — Ödeme ekranı etkileşimleri

   Bu dosya YALNIZCA sunucunun ürettiği sonuçları gösterir. Fiyat, KDV veya
   indirim hesabı burada YAPILMAZ; her değişiklikte /Checkout/Quote çağrılır
   ve ekrana sunucudan gelen biçimlenmiş tutarlar yazılır.

   JavaScript kapalıyken sayfa yine çalışır: kupon ve dönem güncellemesi
   formun "recalc" düğmeleriyle sunucuya gider (bkz. Index.cshtml <noscript>).
   ================================================================ */
(function () {
    'use strict';

    var form = document.getElementById('ea-checkout-form');
    if (!form) return;

    var statusRegion = document.getElementById('ea-checkout-status');
    var submitButton = document.getElementById('ea-checkout-submit');
    var couponInput = document.getElementById('CouponCode');
    var couponButton = document.getElementById('ea-coupon-apply');
    var couponStatus = document.getElementById('CouponCode-status');
    var companyToggle = document.getElementById('IsCompany');
    var companyFields = document.getElementById('ea-company-fields');
    var countrySelect = document.getElementById('Country');
    var phoneInput = document.getElementById('Phone');
    var dialCodeLabel = document.getElementById('ea-dial-code');
    var dialCodeSr = document.getElementById('ea-dial-code-sr');
    var citySelect = form.querySelector('[data-city-select]');
    var cityInput = form.querySelector('[data-city-input]');
    var quoteUrl = form.getAttribute('data-quote-url');

    var TURKEY = 'TR';

    /* Ekran okuyucu duyurusu. Aynı metin art arda gelirse okunmayabilir,
       bu yüzden önce boşaltılır. */
    function announce(message) {
        if (!statusRegion) return;
        statusRegion.textContent = '';
        window.setTimeout(function () { statusRegion.textContent = message; }, 60);
    }

    function antiForgeryToken() {
        var field = form.querySelector('input[name="__RequestVerificationToken"]');
        return field ? field.value : '';
    }

    /* ── Kurumsal fatura alanları ────────────────────────────────── */
    function syncCompanyFields() {
        if (!companyToggle || !companyFields) return;

        var on = companyToggle.checked;
        companyFields.style.display = on ? '' : 'none';
        companyToggle.setAttribute('aria-expanded', on ? 'true' : 'false');

        /* Gizliyken required kalmamalı — tarayıcı görünmeyen alanı
           odaklayamaz ve gönderim sessizce engellenir. */
        ['CompanyName', 'TaxOffice', 'TaxNumber'].forEach(function (id) {
            var field = document.getElementById(id);
            if (!field) return;
            if (on) field.setAttribute('required', 'required');
            else field.removeAttribute('required');
        });

        announce(on ? 'Kurumsal fatura alanları açıldı.' : 'Kurumsal fatura alanları kapatıldı.');
    }

    if (companyToggle) {
        companyToggle.addEventListener('change', syncCompanyFields);
        syncCompanyFields();
    }

    /* ── Ülkeye bağlı alanlar: telefon kodu, il listesi, KDV ─────── */

    function selectedCountryOption() {
        return countrySelect ? countrySelect.options[countrySelect.selectedIndex] : null;
    }

    function selectedCountryCode() {
        return countrySelect ? countrySelect.value : TURKEY;
    }

    /* Ülke değişince: ön ek güncellenir, Türkiye'de il listesi açılır,
       diğer ülkelerde şehir serbest metne döner. İki alan da "City" adını
       taşıdığı için yalnızca etkin olan gönderilir — diğeri disabled kalır. */
    function syncCountryDependentFields(options) {
        if (!countrySelect) return;

        var option = selectedCountryOption();
        var dial = option ? option.getAttribute('data-dial') : '90';
        var isTurkey = selectedCountryCode() === TURKEY;

        if (dialCodeLabel) dialCodeLabel.textContent = '+' + dial;
        if (dialCodeSr) dialCodeSr.textContent = '+' + dial;

        if (phoneInput && option) {
            phoneInput.setAttribute('data-phone-digits', option.getAttribute('data-digits') || '0');
            /* Kullanıcı ülke kodunu da yazdıysa ön ekle çakışmasın diye ayıklanır. */
            var digits = phoneInput.value.replace(/\D/g, '');
            if (digits.indexOf(dial) === 0) phoneInput.value = digits.slice(dial.length);
        }

        if (citySelect && cityInput) {
            citySelect.style.display = isTurkey ? '' : 'none';
            citySelect.disabled = !isTurkey;
            if (isTurkey) citySelect.setAttribute('required', 'required');
            else citySelect.removeAttribute('required');

            cityInput.style.display = isTurkey ? 'none' : '';
            cityInput.disabled = isTurkey;
            if (isTurkey) cityInput.removeAttribute('required');
            else cityInput.setAttribute('required', 'required');

            /* Alan tipi değiştiğinde eski değer taşınmaz — bir ülkenin şehri
               diğerinin il listesinde geçerli olmaz. */
            if (isTurkey) cityInput.value = '';
            else citySelect.value = '';

            var cityLabel = form.querySelector('label[for="City"]');
            if (cityLabel) cityLabel.textContent = isTurkey ? 'İl' : 'Şehir';
            /* Etiket "City" id'sine bağlı; serbest metin alanı için hedefi taşı. */
            if (cityLabel) cityLabel.setAttribute('for', isTurkey ? 'City' : 'CityFree');
        }

        /* KDV oranı ülkeye bağlı olduğu için özet sunucudan yenilenir. */
        if (!options || options.refreshQuote !== false) refreshQuote({});
    }

    if (countrySelect) {
        countrySelect.addEventListener('change', function () { syncCountryDependentFields(); });
        /* İlk yüklemede yalnızca alanlar hizalanır; özet zaten sunucudan geldi. */
        syncCountryDependentFields({ refreshQuote: false });
    }

    /* ── Alan bazlı doğrulama mesajları ──────────────────────────── */
    function errorSlot(field) {
        /* Serbest metin şehir alanı, il seçimiyle aynı hata yuvasını paylaşır. */
        var key = field.id === 'CityFree' ? 'City' : field.id;
        return document.getElementById(key + '-error');
    }

    function setFieldError(field, message) {
        var slot = errorSlot(field);
        if (slot) slot.textContent = message;
        field.classList.add('is-invalid');
        field.setAttribute('aria-invalid', 'true');
    }

    function clearFieldError(field) {
        var slot = errorSlot(field);
        if (slot) slot.textContent = '';
        field.classList.remove('is-invalid');
        field.setAttribute('aria-invalid', 'false');
    }

    /* Tarayıcının kendi kısıt doğrulaması kullanılır; mesaj Türkçeleştirilir. */
    function messageFor(field) {
        if (field.validity.valueMissing) return 'Bu alan zorunludur.';
        if (field.validity.typeMismatch && field.type === 'email') return 'Geçerli bir e-posta adresi girin.';
        if (field.validity.tooShort) return 'Girilen değer çok kısa.';
        if (field.validity.patternMismatch) return 'Girilen değer geçerli değil.';
        return 'Bu alanı kontrol edin.';
    }

    /* Sunucudaki BillingContactRules.IsValidEmail ile aynı kural: tek "@",
       alan adında en az bir nokta ve 2+ harfli uzantı. Nihai karar sunucudadır. */
    var EMAIL_RE = /^[A-Za-z0-9](?:[A-Za-z0-9._%+-]*[A-Za-z0-9])?@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}$/;

    function emailError(value) {
        var v = (value || '').trim();
        if (!v) return 'E-posta zorunludur.';
        if ((v.match(/@/g) || []).length !== 1) return 'E-posta adresi tam olarak bir "@" içermelidir.';
        if (v.indexOf('..') !== -1) return 'E-posta adresinde art arda nokta olamaz.';
        if (!EMAIL_RE.test(v)) return 'Geçerli bir e-posta adresi girin (örnek: ad@alanadi.com).';
        return null;
    }

    /* Telefon kuralı ülkeye bağlıdır: ülke için sabit uzunluk tanımlıysa
       tam o kadar hane, yoksa 6–14 hane. Ülke kodu ön ekte durur, sayılmaz. */
    function phoneError(value) {
        var digits = (value || '').replace(/\D/g, '').replace(/^0+/, '');
        if (!digits) return 'Telefon zorunludur.';

        var expected = parseInt(phoneInput ? phoneInput.getAttribute('data-phone-digits') : '0', 10) || 0;
        var dial = dialCodeLabel ? dialCodeLabel.textContent : '';

        if (expected > 0 && digits.length !== expected) {
            return 'Telefon numarası ' + dial + ' sonrası ' + expected + ' haneli olmalıdır.';
        }
        if (expected === 0 && (digits.length < 6 || digits.length > 14)) {
            return 'Telefon numarası ' + dial + ' sonrası 6–14 hane olmalıdır.';
        }
        return null;
    }

    function validateField(field) {
        if (field.disabled || field.offsetParent === null) return true;

        /* Ülkeye bağlı kurallar tarayıcının genel kontrolünden önce gelir. */
        if (field === phoneInput) {
            var pErr = phoneError(field.value);
            if (pErr) { setFieldError(field, pErr); return false; }
            clearFieldError(field);
            return true;
        }

        if (field.id === 'Email') {
            var eErr = emailError(field.value);
            if (eErr) { setFieldError(field, eErr); return false; }
            clearFieldError(field);
            return true;
        }

        if (field.checkValidity()) {
            clearFieldError(field);
            return true;
        }
        setFieldError(field, messageFor(field));
        return false;
    }

    /* Kullanıcı alandan çıkınca doğrula — yazarken kızmaz, çıkınca uyarır. */
    form.querySelectorAll('input, select, textarea').forEach(function (field) {
        if (field.type === 'hidden' || field.type === 'radio') return;
        field.addEventListener('blur', function () { validateField(field); });
        field.addEventListener('input', function () {
            if (field.classList.contains('is-invalid')) validateField(field);
        });
    });

    /* ── Sipariş özetini sunucudan yenile ────────────────────────── */
    var quoteInFlight = false;

    function selectedPeriod() {
        var checked = form.querySelector('input[name="BillingPeriod"]:checked');
        return checked ? checked.value : 'Monthly';
    }

    function setText(selector, value) {
        var node = form.querySelector(selector);
        if (node && typeof value === 'string') node.textContent = value;
    }

    function applyQuote(data) {
        setText('[data-summary="subtotal"]', data.subtotal);
        setText('[data-summary="tax"]', data.tax);
        setText('[data-summary="total"]', data.total);
        setText('[data-summary="discount"]', data.discount);
        setText('[data-summary="taxLabel"]', data.taxLabel);
        setText('[data-summary-row="discount"] .ea-summary-row__label', data.discountLabel);

        var subtotalLabel = form.querySelector('#ea-summary-rows .ea-summary-row:first-child .ea-summary-row__label');
        if (subtotalLabel) subtotalLabel.textContent = data.subtotalLabel;

        var discountRow = form.querySelector('[data-summary-row="discount"]');
        if (discountRow) discountRow.style.display = data.hasDiscount ? '' : 'none';

        var label = form.querySelector('[data-submit-label]');
        if (label) label.textContent = data.submitLabel;

        if (couponStatus) {
            if (data.hasDiscount) {
                couponStatus.textContent = data.couponMessage || '';
                couponStatus.style.color = 'var(--success-deep)';
            } else if (data.couponAttempted) {
                couponStatus.textContent = data.couponMessage || 'Bu kod geçerli değil.';
                couponStatus.style.color = '';
            } else {
                couponStatus.textContent = '';
            }
        }

        if (data.couponAttempted) {
            announce(data.hasDiscount
                ? 'İndirim uygulandı. Yeni toplam ' + data.total
                : (data.couponMessage || 'Kod geçerli değil.'));
        } else {
            announce('Sipariş özeti güncellendi. Toplam ' + data.total);
        }
    }

    function refreshQuote(options) {
        if (!quoteUrl || quoteInFlight) return;
        quoteInFlight = true;

        var includeCoupon = options && options.includeCoupon;
        if (couponButton && includeCoupon) {
            couponButton.disabled = true;
            couponButton.textContent = 'Kontrol ediliyor…';
        }

        var body = new URLSearchParams();
        body.append('planId', form.querySelector('input[name="PlanId"]').value);
        body.append('billingPeriod', selectedPeriod());
        body.append('couponCode', couponInput ? couponInput.value.trim() : '');
        /* KDV oranı fatura ülkesine bağlı — özet her zaman seçili ülkeyle hesaplanır. */
        body.append('countryCode', selectedCountryCode());

        fetch(quoteUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': antiForgeryToken()
            },
            body: body.toString(),
            credentials: 'same-origin'
        })
            .then(function (response) {
                if (!response.ok) throw new Error('quote-failed');
                return response.json();
            })
            .then(applyQuote)
            .catch(function () {
                /* Ağ hatası: sayfadaki tutarlar sunucudan gelen son doğru
                   değerlerdir, bozulmaz. Kullanıcıya durum bildirilir. */
                if (couponStatus) {
                    couponStatus.textContent = 'Bağlantı hatası. Tutarlar güncellenemedi, lütfen tekrar deneyin.';
                    couponStatus.style.color = '';
                }
                announce('Bağlantı hatası. Sipariş özeti güncellenemedi.');
            })
            .then(function () {
                quoteInFlight = false;
                if (couponButton && includeCoupon) {
                    couponButton.disabled = false;
                    couponButton.textContent = 'Uygula';
                }
            });
    }

    if (couponButton) {
        couponButton.addEventListener('click', function (event) {
            event.preventDefault();
            refreshQuote({ includeCoupon: true });
        });
    }

    /* Kupon alanında Enter, formu göndermek yerine kuponu uygular. */
    if (couponInput) {
        couponInput.addEventListener('keydown', function (event) {
            if (event.key !== 'Enter') return;
            event.preventDefault();
            refreshQuote({ includeCoupon: true });
        });
    }

    form.querySelectorAll('[data-period-radio]').forEach(function (radio) {
        radio.addEventListener('change', function () { refreshQuote({}); });
    });

    /* ── Gönderim ────────────────────────────────────────────────── */
    var submitting = false;

    form.addEventListener('submit', function (event) {
        /* "Uygula" / "Dönemi güncelle" düğmeleri ödemeyi başlatmaz. */
        var trigger = event.submitter;
        if (trigger && trigger.value === 'recalc') return;

        if (submitting) {
            event.preventDefault();
            return;
        }

        /* Doğrulama aşaması */
        var firstInvalid = null;
        form.querySelectorAll('input, select, textarea').forEach(function (field) {
            if (field.type === 'hidden') return;
            if (!validateField(field) && !firstInvalid) firstInvalid = field;
        });

        var terms = document.getElementById('AcceptTerms');
        if (terms && !terms.checked) {
            var termsSlot = document.getElementById('AcceptTerms-error');
            if (termsSlot) termsSlot.textContent = 'Devam etmek için sözleşmeyi onaylamanız gerekir.';
            terms.setAttribute('aria-invalid', 'true');
            if (!firstInvalid) firstInvalid = terms;
        } else if (terms) {
            var slot = document.getElementById('AcceptTerms-error');
            if (slot) slot.textContent = '';
            terms.setAttribute('aria-invalid', 'false');
        }

        if (firstInvalid) {
            event.preventDefault();
            firstInvalid.focus();
            announce('Form gönderilemedi. Lütfen işaretlenen alanları düzeltin.');
            return;
        }

        /* Gönderim aşaması: düğme kilitlenir — çift tıklama ikinci bir
           sipariş başlatamaz. Sunucudaki idempotency anahtarı bu korumayı
           tarayıcıdan bağımsız olarak da sağlar. */
        submitting = true;
        if (submitButton) {
            submitButton.disabled = true;
            submitButton.innerHTML =
                '<span class="ea-spinner" aria-hidden="true"></span>' +
                '<span>Ödeme sayfasına yönlendiriliyorsunuz…</span>';
        }
        announce('Ödeme başlatılıyor. Sağlayıcının güvenli sayfasına yönlendiriliyorsunuz.');
    });

    /* Kullanıcı sağlayıcı sayfasından geri tuşuyla dönerse form yeniden
       kullanılabilir olmalı; aksi hâlde düğme kilitli kalır. */
    window.addEventListener('pageshow', function (event) {
        if (!event.persisted) return;
        submitting = false;
        if (submitButton) {
            submitButton.disabled = false;
            var label = submitButton.querySelector('[data-submit-label]');
            if (!label) location.reload();
        }
    });
})();

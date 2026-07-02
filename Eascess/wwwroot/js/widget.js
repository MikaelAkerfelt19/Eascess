/**
 * Eascess Widget v1.2
 * WCAG 2.2 uyumlu erişilebilirlik katmanı
 */
(function () {
    'use strict';

    var SELF = document.currentScript || document.querySelector('script[data-key]');
    if (!SELF) return;

    var LICENSE_KEY    = SELF.getAttribute('data-key');
    var API_BASE       = (SELF.getAttribute('data-api') || '').replace(/\/$/, '');
    var INLINE_CONFIG  = SELF.getAttribute('data-config');
    var STORAGE_KEY    = 'eascess-prefs-' + (LICENSE_KEY || 'demo');
    var CURRENT_DOMAIN = window.location.hostname;

    if (!LICENSE_KEY && !INLINE_CONFIG) return;

    function apiUrl(path) { return API_BASE ? API_BASE + path : path; }

    // ── Kullanım Analitik Logu ───────────────────────────────────────────────
    var _logThrottle = {};
    function logEvent(type, feature) {
        var throttleKey = type + ':' + (feature || '');
        var now = Date.now();
        if (_logThrottle[throttleKey] && now - _logThrottle[throttleKey] < 5000) return;
        _logThrottle[throttleKey] = now;

        try {
            fetch(apiUrl('/api/widget/log'), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ licenseKey: LICENSE_KEY, event: type, feature: feature || null }),
                keepalive: true,
            }).catch(function () {});
        } catch (e) {}
    }

    // ── Bootstrapper ─────────────────────────────────────────────────────────
    function start() {
        if (INLINE_CONFIG) {
            try { buildWidget(sanitizeConfig(JSON.parse(INLINE_CONFIG))); }
            catch (e) { buildWidget(sanitizeConfig({})); }
            return;
        }
        fetch(apiUrl('/api/license/validate?key=' + LICENSE_KEY + '&domain=' + CURRENT_DOMAIN))
            .then(function (r) { return r.json(); })
            .then(function (result) {
                if (!result.valid) {
                    if (result.reason === 'plan_expired')
                        console.warn('[Eascess] Plan limiti: bu domain mevcut plan kapsaminda degil. Widget devre disi birakildi — plani yukseltin.');
                    else
                        console.warn('[Eascess] Lisans geçersiz.');
                    return;
                }
                fetchConfig();
            })
            .catch(function () {
                if (CURRENT_DOMAIN === 'localhost' || CURRENT_DOMAIN === '127.0.0.1') fetchConfig();
                else console.warn('[Eascess] Lisans doğrulaması başarısız.');
            });
    }

    function fetchConfig() {
        fetch(apiUrl('/api/widget/config?key=' + LICENSE_KEY))
            .then(function (r) { if (!r.ok) throw new Error(); return r.json(); })
            .then(function (cfg) { buildWidget(sanitizeConfig(cfg)); })
            .catch(function () {
                if (CURRENT_DOMAIN === 'localhost' || CURRENT_DOMAIN === '127.0.0.1')
                    buildWidget({ themeColor: '#38bdf8', position: 'bottom-right', language: 'tr', isAiEnabled: false });
            });
    }

    // ── Tercihler ────────────────────────────────────────────────────────────
    var DEFAULT_PREFS = {
        fontSize: 0,
        contrast: 'normal',
        grayscale: false,
        highlightLinks: false,
        pauseAnimations: false,
        bigCursor: false,
        readingGuide: false,
        textSpacing: false,
        dyslexiaFont: false,
        epilepsyMode: false,
        visionMode: false,
        cognitiveMode: false,
        adhdMode: false,
        screenReaderMode: false,
        highlightHeadings: false,
        lineHeight: 'default',
        textAlign: 'default',
        hideImages: false,
        readingMask: false,
        bgColor: '',
        textColor: '',
        headingColor: '',
        tts: false,
    };

    function loadPrefs() {
        try { return Object.assign({}, DEFAULT_PREFS, JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}')); }
        catch (e) { return Object.assign({}, DEFAULT_PREFS); }
    }

    function savePrefs(p) { try { localStorage.setItem(STORAGE_KEY, JSON.stringify(p)); } catch (e) {} }

    // ── Renk paletleri ───────────────────────────────────────────────────────
    var BG_COLORS      = ['#ffffff', '#fffde7', '#e8f5e9', '#e3f2fd', '#f3e5f5', '#fce4ec', '#1a1a2e'];
    var TEXT_COLORS    = ['#212121', '#f5f5f5', '#1a237e', '#1b5e20', '#b71c1c', '#4a148c'];
    var HEADING_COLORS = ['#212121', '#f97316', '#38bdf8', '#4ade80', '#f472b6', '#a78bfa', '#fbbf24'];

    // ── CSS uygula ───────────────────────────────────────────────────────────
    function applyPrefs(prefs) {
        var styleEl = document.getElementById('eascess-styles');
        if (!styleEl) {
            styleEl = document.createElement('style');
            styleEl.id = 'eascess-styles';
            document.head.appendChild(styleEl);
        }

        var html  = document.documentElement;
        var rules = [];

        // Font boyutu
        if (prefs.fontSize !== 0)
            rules.push('html{font-size:' + (100 + prefs.fontSize * 10) + '%!important}');

        // Filtreler (birleştirilmiş)
        var filters = [];
        if (prefs.contrast === 'high')     filters.push('contrast(160%)');
        if (prefs.contrast === 'negative') filters.push('invert(100%) hue-rotate(180deg)');
        if (prefs.grayscale)               filters.push('grayscale(100%)');
        if (prefs.epilepsyMode)            filters.push('brightness(0.88)');
        if (prefs.visionMode)              filters.push('contrast(130%) brightness(1.1)');
        if (filters.length)
            rules.push('html{filter:' + filters.join(' ') + '!important}');

        // Link vurgusu
        if (prefs.highlightLinks)
            rules.push('a{outline:2px solid #f97316!important;outline-offset:2px!important;background:rgba(249,115,22,.12)!important}');

        // Animasyon durdur
        if (prefs.pauseAnimations || prefs.epilepsyMode || prefs.adhdMode)
            rules.push('*,*::before,*::after{animation:none!important;transition:none!important}');

        // Epilepsi: scroll
        if (prefs.epilepsyMode)
            rules.push('html{scroll-behavior:auto!important}');

        // Büyük imleç
        html.style.cursor = prefs.bigCursor
            ? 'url("data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' width=\'32\' height=\'32\' viewBox=\'0 0 32 32\'><path fill=\'%23fff\' stroke=\'%23000\' stroke-width=\'2\' d=\'M8 2l16 16-6.5 1.5L14 28 8 2z\'/></svg>") 0 0, auto'
            : '';

        // Metin aralığı
        if (prefs.textSpacing)
            rules.push('*{letter-spacing:.12em!important;word-spacing:.16em!important;line-height:1.8!important}');

        // Disleksi fontu
        if (prefs.dyslexiaFont)
            rules.push('*{font-family:Arial,"Helvetica Neue",sans-serif!important;font-weight:500!important}');

        // Bilişsel mod
        if (prefs.cognitiveMode) {
            rules.push('*{font-family:Arial,"Helvetica Neue",sans-serif!important;line-height:1.9!important}');
            rules.push('body *{background-image:none!important}');
            rules.push('body{max-width:800px!important;margin-left:auto!important;margin-right:auto!important}');
        }

        // DEHB modu
        if (prefs.adhdMode) {
            rules.push('img[src]:not([role="presentation"]){opacity:.4!important}');
            rules.push(':focus{outline:3px solid #f97316!important;outline-offset:3px!important;box-shadow:0 0 0 6px rgba(249,115,22,.25)!important}');
        }

        // Başlık vurgulama
        if (prefs.highlightHeadings)
            rules.push('h1,h2,h3,h4,h5,h6{border-left:4px solid #f97316!important;padding-left:.5em!important;background:rgba(249,115,22,.08)!important}');

        // Satır yüksekliği
        if (prefs.lineHeight === 'medium') rules.push('*{line-height:1.7!important}');
        else if (prefs.lineHeight === 'wide') rules.push('*{line-height:2.0!important}');

        // Metin hizalama
        if (prefs.textAlign !== 'default')
            rules.push('p,li,td,th,div,span{text-align:' + prefs.textAlign + '!important}');

        // Görselleri gizle
        if (prefs.hideImages) rules.push('img{opacity:0!important}');

        // Arka plan rengi
        if (prefs.bgColor)
            rules.push('body,body *{background-color:' + prefs.bgColor + '!important}');

        // Metin rengi
        if (prefs.textColor)
            rules.push('body,body *{color:' + prefs.textColor + '!important}');

        // Başlık rengi
        if (prefs.headingColor)
            rules.push('h1,h2,h3,h4,h5,h6{color:' + prefs.headingColor + '!important}');

        styleEl.textContent = rules.join('\n');

        // Odak halkası
        if (!document.getElementById('eascess-focus-styles')) {
            var fs = document.createElement('style');
            fs.id = 'eascess-focus-styles';
            fs.textContent = ':focus-visible{outline:3px solid #f97316!important;outline-offset:3px!important}';
            document.head.appendChild(fs);
        }

        manageReadingGuide(prefs.readingGuide && !prefs.readingMask);
        manageReadingMask(prefs.readingMask && !prefs.readingGuide);
        manageScreenReaderMode(prefs.screenReaderMode);
        manageTts(prefs.tts);
    }

    // ── Okuma rehberi ────────────────────────────────────────────────────────
    var guideEl = null;
    function manageReadingGuide(active) {
        if (active) {
            if (!guideEl) {
                guideEl = document.createElement('div');
                guideEl.setAttribute('aria-hidden', 'true');
                guideEl.style.cssText = 'position:fixed;left:0;width:100%;height:36px;background:rgba(249,115,22,.25);border-top:2px solid #f97316;border-bottom:2px solid #f97316;pointer-events:none;z-index:2147483646;transition:top .05s linear';
                document.body.appendChild(guideEl);
            }
            document.addEventListener('mousemove', moveGuide);
        } else {
            document.removeEventListener('mousemove', moveGuide);
            if (guideEl) { guideEl.remove(); guideEl = null; }
        }
    }
    function moveGuide(e) { if (guideEl) guideEl.style.top = (e.clientY - 18) + 'px'; }

    // ── Okuma maskesi ────────────────────────────────────────────────────────
    var maskTopEl = null, maskBotEl = null;
    function manageReadingMask(active) {
        if (active) {
            if (!maskTopEl) {
                var base = 'position:fixed;left:0;width:100%;pointer-events:none;z-index:2147483645;background:rgba(0,0,0,.75);';
                maskTopEl = document.createElement('div');
                maskTopEl.setAttribute('aria-hidden', 'true');
                maskTopEl.style.cssText = base + 'top:0;height:40%';
                maskBotEl = document.createElement('div');
                maskBotEl.setAttribute('aria-hidden', 'true');
                maskBotEl.style.cssText = base + 'bottom:0;height:55%';
                document.body.appendChild(maskTopEl);
                document.body.appendChild(maskBotEl);
            }
            document.addEventListener('mousemove', moveMask);
        } else {
            document.removeEventListener('mousemove', moveMask);
            if (maskTopEl) { maskTopEl.remove(); maskTopEl = null; }
            if (maskBotEl) { maskBotEl.remove(); maskBotEl = null; }
        }
    }
    function moveMask(e) {
        var stripH = 60, y = e.clientY, vh = window.innerHeight;
        if (maskTopEl) maskTopEl.style.height = Math.max(0, y - stripH / 2) + 'px';
        if (maskBotEl) maskBotEl.style.height = Math.max(0, vh - y - stripH / 2) + 'px';
    }

    // ── Ekran okuyucu modu ───────────────────────────────────────────────────
    var srCleanupFns = [];
    function manageScreenReaderMode(active) {
        srCleanupFns.forEach(function (fn) { fn(); });
        srCleanupFns = [];
        // Önceki IIFE örneğinden kalan orphan elementleri temizle
        ['eascess-sr-banner', 'eascess-skip-nav'].forEach(function (id) {
            var el = document.getElementById(id); if (el) el.remove();
        });
        if (!active) return;

        var pageLang = document.documentElement.lang || 'tr';
        var isTr = pageLang.indexOf('en') !== 0;

        // Görünür "mod aktif" bandı
        var banner = document.createElement('div');
        banner.id = 'eascess-sr-banner';
        banner.setAttribute('aria-live', 'polite');
        banner.textContent = isTr ? '♿ Ekran Okuyucu Modu Aktif' : '♿ Screen Reader Mode Active';
        banner.style.cssText = 'position:fixed;top:0;left:0;right:0;background:#1e40af;color:#fff;text-align:center;font-size:.8rem;font-weight:700;padding:.35rem;z-index:2147483646;letter-spacing:.3px';
        document.body.insertBefore(banner, document.body.firstChild);
        srCleanupFns.push(function () { banner.remove(); });

        // Skip nav
        var skipNav = document.createElement('a');
        skipNav.href = '#';
        skipNav.id = 'eascess-skip-nav';
        skipNav.textContent = isTr ? 'Ana içeriğe geç' : 'Skip to main content';
        skipNav.style.cssText = 'position:fixed;top:36px;left:10px;z-index:2147483647;background:#f97316;color:#fff;padding:.5rem 1rem;border-radius:0 0 8px 8px;font-weight:700;transform:translateY(-150%);transition:transform .15s';
        skipNav.addEventListener('focus', function () { skipNav.style.transform = 'translateY(0)'; });
        skipNav.addEventListener('blur',  function () { skipNav.style.transform = 'translateY(-150%)'; });
        skipNav.addEventListener('click', function (e) {
            e.preventDefault();
            var main = document.querySelector('main, [role="main"], #main, #content');
            if (main) { main.setAttribute('tabindex', '-1'); main.focus(); }
        });
        document.body.insertBefore(skipNav, banner.nextSibling);
        srCleanupFns.push(function () { skipNav.remove(); });

        // Dekoratif görseller
        document.querySelectorAll('img').forEach(function (img) {
            var alt = img.getAttribute('alt');
            if ((alt === '' || alt === null) && !img.hasAttribute('role')) {
                img.setAttribute('role', 'presentation');
                srCleanupFns.push(function () { img.removeAttribute('role'); });
            }
        });

        // Boş anchor'lar — yalnızca gerçek alt metni olan img label sayılır
        document.querySelectorAll('a[href]').forEach(function (a) {
            var text = (a.textContent || '').trim();
            var hasLabel = a.getAttribute('aria-label') || a.getAttribute('aria-labelledby') || a.querySelector('img[alt]:not([alt=""])');
            if (!text && !hasLabel) {
                a.setAttribute('aria-label', (isTr ? 'Bağlantı: ' : 'Link: ') + (a.getAttribute('href') || ''));
                srCleanupFns.push(function () { a.removeAttribute('aria-label'); });
            }
        });
    }

    // ── Sesli Okuma (TTS) ─────────────────────────────────────────────────────
    var _ttsClickHandler = null;
    var _ttsStyle = null;
    function manageTts(active) {
        if (_ttsClickHandler) {
            document.removeEventListener('click', _ttsClickHandler, true);
            _ttsClickHandler = null;
        }
        if (_ttsStyle) { _ttsStyle.remove(); _ttsStyle = null; }
        // Önceki IIFE örneğinden kalan orphan elementi temizle
        var orphanTts = document.getElementById('eascess-tts-styles');
        if (orphanTts) orphanTts.remove();
        if (window.speechSynthesis) window.speechSynthesis.cancel();
        if (!active) return;

        _ttsStyle = document.createElement('style');
        _ttsStyle.id = 'eascess-tts-styles';
        _ttsStyle.textContent =
            'p:hover,h1:hover,h2:hover,h3:hover,h4:hover,h5:hover,h6:hover,' +
            'li:hover,td:hover,th:hover,blockquote:hover,figcaption:hover{' +
            'outline:2px dashed #f97316!important;cursor:copy!important}';
        document.head.appendChild(_ttsStyle);

        _ttsClickHandler = function (e) {
            var el = e.target;
            while (el && el !== document.body) {
                var tag = (el.tagName || '').toLowerCase();
                if (/^(p|h[1-6]|li|td|th|blockquote|figcaption)$/.test(tag)) break;
                el = el.parentElement;
            }
            if (!el || el === document.body) return;
            var text = (el.textContent || '').trim();
            if (!text || !window.speechSynthesis) return;
            window.speechSynthesis.cancel();
            var utt = new SpeechSynthesisUtterance(text);
            var lang = document.documentElement.lang || 'tr';
            utt.lang = lang.indexOf('en') === 0 ? 'en-US' : 'tr-TR';
            window.speechSynthesis.speak(utt);
        };
        document.addEventListener('click', _ttsClickHandler, true);
    }

    // ── AI Caption badge ─────────────────────────────────────────────────────
    function addCaptionBadge(img, altText) {
        var key = img.currentSrc || img.src;
        var existing = img.parentElement && img.parentElement.querySelector('.eascess-caption[data-for="' + key + '"]');
        if (existing) { existing.textContent = altText; return; }
        var badge = document.createElement('div');
        badge.className = 'eascess-caption';
        badge.setAttribute('data-for', key);
        badge.textContent = altText;
        badge.style.cssText = 'display:block;font-size:.75rem;color:#374151;background:rgba(249,115,22,.1);border:1px solid rgba(249,115,22,.3);border-radius:0 0 6px 6px;padding:.25rem .5rem;margin-top:2px;max-width:' + (img.offsetWidth || 300) + 'px';
        if (img.parentElement) img.parentElement.insertBefore(badge, img.nextSibling);
    }

    // ── Widget inşası ────────────────────────────────────────────────────────
    function buildWidget(cfg) {
        var existing = document.getElementById('eascess-widget-host');
        if (existing) existing.remove();

        var prefs = loadPrefs();
        applyPrefs(prefs);

        var host = document.createElement('div');
        host.id = 'eascess-widget-host';
        host.setAttribute('role', 'complementary');
        host.setAttribute('aria-label', t(cfg, 'regionLabel'));
        host.style.cssText = 'position:fixed;z-index:2147483647';
        document.body.appendChild(host);

        var shadow = host.attachShadow({ mode: 'closed' });
        var pos    = cfg.position || 'bottom-right';
        var sides  = pos.split('-');
        var posStyle = sides[0] + ':20px;' + sides[1] + ':20px;';

        shadow.innerHTML = buildShadowHTML(cfg, prefs, posStyle);
        bindEvents(shadow, cfg, prefs);
        if (typeof window._widgetReady === 'function') window._widgetReady();
    }

    // ── i18n ─────────────────────────────────────────────────────────────────
    var LABELS = {
        tr: {
            regionLabel:'Erişilebilirlik Widget\'ı', open:'Erişilebilirlik Menüsünü Aç',
            title:'Erişilebilirlik', reset:'Sıfırla', close:'Kapat',
            fontSize:'Yazı Boyutu', contrast:'Kontrast',
            contrastNormal:'Normal', contrastHigh:'Yüksek', contrastNeg:'Negatif',
            grayscale:'Gri Tonlama', links:'Bağlantıları Vurgula',
            animations:'Animasyonları Durdur', cursor:'Büyük İmleç',
            guide:'Okuma Rehberi', spacing:'Metin Aralığı', dyslexia:'Okunabilir Font',
            aiAltText:'AI Alt Metin Üret', aiScanning:' görsel taranıyor...',
            aiDone:' görsel için alt metin oluşturuldu',
            aiNone:'Alt metni eksik görsel bulunamadı', aiError:'AI tarama şu an kullanılamıyor',
            aiQuota:'Aylık AI tarama kotanız doldu', aiDisabled:'Bu site için AI tarama devre dışı',
            sectionDisplay:'Görünüm', sectionLayout:'Metin Düzeni',
            sectionNavigation:'Gezinti & Odak', sectionAccessibility:'Erişilebilirlik Modları',
            sectionColors:'Renk Ayarları',
            epilepsy:'Epilepsi Güvenli Mod', vision:'Görme Engelli Modu',
            cognitive:'Bilişsel Erişilebilirlik', adhd:'DEHB Dostu Mod',
            screenReader:'Ekran Okuyucu Modu', highlightHeadings:'Başlıkları Vurgula',
            lineHeight:'Satır Yüksekliği', lineDefault:'Varsayılan', lineMedium:'Orta', lineWide:'Geniş',
            textAlign:'Metin Hizalama', alignDefault:'Sol', alignCenter:'Orta', alignRight:'Sağ', alignJustify:'İki Yana',
            hideImages:'Görselleri Gizle', readingMask:'Okuma Maskesi',
            bgColor:'Arka Plan', textColor:'Metin Rengi', headingColor:'Başlık Rengi',
            tts:'Sesli Okuma',
        },
        en: {
            regionLabel:'Accessibility Widget', open:'Open Accessibility Menu',
            title:'Accessibility', reset:'Reset', close:'Close',
            fontSize:'Font Size', contrast:'Contrast',
            contrastNormal:'Normal', contrastHigh:'High', contrastNeg:'Negative',
            grayscale:'Grayscale', links:'Highlight Links',
            animations:'Pause Animations', cursor:'Big Cursor',
            guide:'Reading Guide', spacing:'Text Spacing', dyslexia:'Readable Font',
            aiAltText:'AI Alt Text Generate', aiScanning:' images scanning...',
            aiDone:' images got alt text',
            aiNone:'No images missing alt text found', aiError:'AI scan is currently unavailable',
            aiQuota:'Monthly AI scan quota exceeded', aiDisabled:'AI scan is disabled for this site',
            sectionDisplay:'Display', sectionLayout:'Text Layout',
            sectionNavigation:'Navigation & Focus', sectionAccessibility:'Accessibility Modes',
            sectionColors:'Color Settings',
            epilepsy:'Epilepsy Safe Mode', vision:'Vision Impaired Mode',
            cognitive:'Cognitive Accessibility', adhd:'ADHD Friendly Mode',
            screenReader:'Screen Reader Mode', highlightHeadings:'Highlight Headings',
            lineHeight:'Line Height', lineDefault:'Default', lineMedium:'Medium', lineWide:'Wide',
            textAlign:'Text Align', alignDefault:'Left', alignCenter:'Center', alignRight:'Right', alignJustify:'Justify',
            hideImages:'Hide Images', readingMask:'Reading Mask',
            bgColor:'Background', textColor:'Text Color', headingColor:'Heading Color',
            tts:'Text to Speech',
        },
    };

    function t(cfg, key) {
        var lang = ((cfg && cfg.language) || 'tr').toLowerCase();
        return (LABELS[lang] || LABELS.tr)[key] || key;
    }

    // ── Shadow DOM HTML ───────────────────────────────────────────────────────
    function buildShadowHTML(cfg, prefs, posStyle) {
        var color = cfg.themeColor || '#38bdf8';
        var l = function (k) { return t(cfg, k); };
        var fDisp = prefs.fontSize === 0 ? '100%' : (100 + prefs.fontSize * 10) + '%';

        // Eascess "Warm Cream" tokens as hex (oklch fallback-safe for older embedded browsers)
        var origin = (cfg.position || 'bottom-right').split('-').join(' ');
        var css = '<style>' +
'@keyframes ea-fab-in{from{opacity:0;transform:scale(.55) translateY(10px)}to{opacity:1;transform:scale(1) translateY(0)}}' +
'*{box-sizing:border-box;margin:0;padding:0;font-family:"Inter",-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif}' +

// ── Tetikleyici buton (FAB) ──
'#ea-toggle{position:fixed;' + posStyle + 'width:56px;height:56px;border-radius:50%;background:' + color + ';border:none;cursor:pointer;display:flex;align-items:center;justify-content:center;box-shadow:0 10px 26px -6px rgba(15,23,42,.4),0 5px 10px -4px rgba(15,23,42,.22),inset 0 0 0 1px rgba(255,255,255,.18);transition:transform .28s cubic-bezier(.22,1,.36,1),box-shadow .28s cubic-bezier(.22,1,.36,1);z-index:2147483647;animation:ea-fab-in .55s cubic-bezier(.16,1,.3,1) both;-webkit-tap-highlight-color:transparent}' +
'#ea-toggle:hover{transform:scale(1.09) translateY(-1px);box-shadow:0 16px 38px -6px ' + color + '99,0 6px 14px -4px rgba(15,23,42,.3),inset 0 0 0 1px rgba(255,255,255,.3)}' +
'#ea-toggle:active{transform:scale(1.02) translateY(0)}' +
'#ea-toggle:focus-visible{outline:3px solid #fff;outline-offset:3px;box-shadow:0 10px 26px -6px rgba(15,23,42,.4),0 0 0 6px ' + color + '55}' +
'#ea-toggle svg{width:30px;height:30px;fill:#fff;pointer-events:none;filter:drop-shadow(0 1px 1px rgba(0,0,0,.2))}' +
'#ea-toggle img{width:40px;height:40px;object-fit:cover;border-radius:50%;pointer-events:none}' +

// ── Panel ──
'#ea-panel{position:fixed;' + posStyle + 'width:332px;max-width:calc(100vw - 32px);background:#faf8f3;border:1px solid #dcd4c6;border-radius:20px;box-shadow:0 24px 60px -14px rgba(15,23,42,.28),0 10px 24px -10px rgba(15,23,42,.14);overflow:hidden;transform:scale(.94) translateY(12px);opacity:0;pointer-events:none;transform-origin:' + origin + ';transition:transform .34s cubic-bezier(.16,1,.3,1),opacity .26s ease;z-index:2147483647;max-height:min(90vh,640px);overflow-y:auto;overscroll-behavior:contain}' +
'#ea-panel.open{transform:scale(1) translateY(0);opacity:1;pointer-events:all}' +

// ── Başlık ──
'.ea-head{display:flex;align-items:center;gap:.65rem;padding:.95rem 1.1rem;border-bottom:1px solid #ebe5da;background:#f4f0e9}' +
'.ea-brandmark{width:32px;height:32px;border-radius:9px;background:linear-gradient(135deg,#35b9bd,#423c89);display:flex;align-items:center;justify-content:center;flex-shrink:0;box-shadow:0 3px 8px -2px rgba(66,60,137,.45)}' +
'.ea-brandmark svg{width:19px;height:19px;fill:#fff}' +
'.ea-head-title{font-size:.97rem;font-weight:700;color:#1b1e25;letter-spacing:-.01em;line-height:1.2;flex:1}' +
'.ea-head-actions{display:flex;gap:.4rem;flex-shrink:0}' +
'.ea-head-btn{background:#ece6db;border:1px solid #dcd4c6;border-radius:9px;color:#464c58;font-size:.72rem;font-weight:600;padding:.4rem .68rem;cursor:pointer;transition:background .18s,color .18s,border-color .18s}' +
'.ea-head-btn:hover{background:#e3dccf;color:#1b1e25;border-color:#cfc6b6}' +
'.ea-head-btn:focus-visible{outline:2px solid ' + color + ';outline-offset:2px}' +

// ── Bölümler ──
'.ea-section{padding:.9rem 1.1rem}' +
'.ea-section+.ea-section{border-top:1px solid #efe9df}' +
'.ea-section-label{font-size:.66rem;font-weight:700;text-transform:uppercase;letter-spacing:.09em;color:#868d99;margin-bottom:.7rem}' +

// ── Yazı boyutu ──
'.ea-font-ctrl{display:flex;align-items:center;gap:.5rem}' +
'.ea-font-btn{width:36px;height:36px;border-radius:10px;background:#f4f0e9;border:1px solid #dcd4c6;color:#1b1e25;font-size:1rem;font-weight:700;cursor:pointer;display:flex;align-items:center;justify-content:center;transition:background .18s,border-color .18s,transform .1s;flex-shrink:0}' +
'.ea-font-btn:hover{background:#fff;border-color:' + color + '}' +
'.ea-font-btn:active{transform:scale(.93)}' +
'.ea-font-btn:focus-visible{outline:2px solid ' + color + ';outline-offset:2px}' +
'.ea-font-display{flex:1;text-align:center;font-size:.85rem;font-weight:600;color:#1b1e25;background:#fff;border:1px solid #ebe5da;border-radius:10px;padding:.45rem}' +

// ── Segmentli kontrol ──
'.ea-seg{display:flex;background:#efe9df;border:1px solid #e3dccf;border-radius:11px;padding:3px;gap:2px}' +
'.ea-seg-btn{flex:1;padding:.42rem;border:none;border-radius:8px;color:#464c58;font-size:.75rem;font-weight:600;cursor:pointer;background:transparent;transition:color .18s,box-shadow .18s}' +
'.ea-seg-btn:hover{color:#1b1e25}' +
'.ea-seg-btn.active{background:' + color + ';color:#fff;box-shadow:0 2px 6px -1px ' + color + '66}' +
'.ea-seg-btn:focus-visible{outline:2px solid ' + color + ';outline-offset:2px}' +
'.ea-seg-label{font-size:.78rem;color:#464c58;margin-bottom:.35rem;margin-top:.6rem;font-weight:500}' +

// ── Anahtar satırları ──
'.ea-row{display:flex;align-items:center;justify-content:space-between;margin:0 -.5rem;padding:.5rem;border-radius:10px;transition:background .15s}' +
'.ea-row:hover{background:#f4f0e9}' +
'.ea-row-label{font-size:.84rem;color:#2a2e37;display:flex;align-items:center;gap:.6rem;cursor:pointer;flex:1}' +
'.ea-row-label svg{width:17px;height:17px;fill:#868d99;flex-shrink:0;transition:fill .15s}' +
'.ea-row:hover .ea-row-label svg{fill:#464c58}' +
'.ea-switch{position:relative;width:42px;height:23px;flex-shrink:0}' +
'.ea-switch input{opacity:0;width:0;height:0;position:absolute}' +
'.ea-slider{position:absolute;inset:0;border-radius:23px;cursor:pointer;background:#d3cab9;transition:background .25s cubic-bezier(.22,1,.36,1)}' +
'.ea-slider::before{content:"";position:absolute;height:17px;width:17px;left:3px;bottom:3px;border-radius:50%;background:#fff;transition:transform .25s cubic-bezier(.22,1,.36,1);box-shadow:0 1px 3px rgba(15,23,42,.28)}' +
'input:checked+.ea-slider{background:' + color + '}' +
'input:checked+.ea-slider::before{transform:translateX(19px)}' +
'.ea-switch input:focus-visible+.ea-slider{outline:2px solid ' + color + ';outline-offset:2px}' +

// ── Renk paletleri ──
'.ea-palette-row{margin-bottom:.6rem}' +
'.ea-palette-row:last-child{margin-bottom:0}' +
'.ea-palette-label{font-size:.78rem;color:#464c58;margin-bottom:.35rem;display:flex;align-items:center;justify-content:space-between;font-weight:500}' +
'.ea-palette-swatches{display:flex;flex-wrap:wrap;gap:.4rem;align-items:center}' +
'.ea-swatch{width:25px;height:25px;border-radius:50%;border:2px solid #dcd4c6;cursor:pointer;transition:transform .18s cubic-bezier(.22,1,.36,1),box-shadow .18s;flex-shrink:0;padding:0}' +
'.ea-swatch:hover{transform:scale(1.18)}' +
'.ea-swatch.selected{border-color:#fff;transform:scale(1.12);box-shadow:0 0 0 2px ' + color + ',0 2px 6px -1px rgba(15,23,42,.3)}' +
'.ea-palette-reset{background:#ece6db;border:1px solid #dcd4c6;color:#636a77;font-size:.85rem;line-height:1;padding:.25rem .5rem;border-radius:7px;cursor:pointer;transition:background .15s,color .15s}' +
'.ea-palette-reset:hover{background:#e3dccf;color:#1b1e25}' +

// ── AI butonu (marka degrade CTA) ──
'.ea-ai-btn{width:100%;padding:.72rem;border:none;border-radius:12px;background:linear-gradient(135deg,' + color + ',#423c89);color:#fff;font-size:.82rem;font-weight:600;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:.45rem;transition:transform .15s cubic-bezier(.22,1,.36,1),box-shadow .2s,opacity .15s;box-shadow:0 4px 12px -2px rgba(66,60,137,.35)}' +
'.ea-ai-btn:hover{transform:translateY(-1px);box-shadow:0 9px 20px -4px rgba(66,60,137,.45)}' +
'.ea-ai-btn:active{transform:translateY(0)}' +
'.ea-ai-btn:focus-visible{outline:2px solid ' + color + ';outline-offset:2px}' +
'.ea-ai-btn:disabled{opacity:.55;cursor:not-allowed;transform:none;box-shadow:none}' +
'.ea-ai-btn svg{width:17px;height:17px;fill:#fff;flex-shrink:0}' +
'.ea-ai-status{font-size:.73rem;color:#464c58;text-align:center;margin-top:.5rem;min-height:1rem}' +

// ── Alt bilgi ──
'.ea-powered{text-align:center;padding:.75rem;font-size:.66rem;color:#868d99;border-top:1px solid #efe9df;background:#f4f0e9}' +
'.ea-powered a{color:#423c89;text-decoration:none;font-weight:600}' +
'.ea-powered a:hover{text-decoration:underline}' +

// ── Kaydırma çubuğu ──
'#ea-panel::-webkit-scrollbar{width:6px}' +
'#ea-panel::-webkit-scrollbar-thumb{background:#d3cab9;border-radius:3px}' +
'#ea-panel::-webkit-scrollbar-thumb:hover{background:#c2b8a5}' +

// ── Hareket azaltma tercihi ──
'@media (prefers-reduced-motion:reduce){#ea-toggle,#ea-panel,.ea-slider,.ea-slider::before,.ea-ai-btn,.ea-swatch,.ea-seg-btn,.ea-row,.ea-font-btn,.ea-head-btn{animation:none!important;transition:none!important}}' +
'</style>';

        // Evrensel erişilebilirlik figürü (Material "accessibility")
        var a11ySvg = '<svg viewBox="0 0 24 24" width="30" height="30" aria-hidden="true" style="pointer-events:none"><path d="M20.5 6c-2.61.7-5.67 1-8.5 1s-5.89-.3-8.5-1L3 8c1.86.5 4 .83 6 1v13h2v-6h2v6h2V9c2-.17 4.14-.5 6-1l-.5-2zM12 6c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2z"/></svg>';
        var brandSvg = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20.5 6c-2.61.7-5.67 1-8.5 1s-5.89-.3-8.5-1L3 8c1.86.5 4 .83 6 1v13h2v-6h2v6h2V9c2-.17 4.14-.5 6-1l-.5-2zM12 6c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2z"/></svg>';
        var fabContent = cfg.logoUrl
            ? '<img src="' + cfg.logoUrl + '" alt="" aria-hidden="true" onerror="this.style.display=\'none\';this.nextElementSibling.style.display=\'\'"><span style="display:none;pointer-events:none">' + a11ySvg + '</span>'
            : a11ySvg;

        var html =
'<button id="ea-toggle" aria-label="' + l('open') + '" aria-expanded="false" aria-controls="ea-panel">' +
  fabContent +
'</button>' +
'<div id="ea-panel" role="dialog" aria-label="' + l('title') + '" aria-modal="true">' +

  '<div class="ea-head">' +
    '<span class="ea-brandmark" aria-hidden="true">' + brandSvg + '</span>' +
    '<span class="ea-head-title">' + (cfg.widgetTitle || l('title')) + '</span>' +
    '<div class="ea-head-actions">' +
      '<button class="ea-head-btn" id="ea-reset" type="button">' + l('reset') + '</button>' +
      '<button class="ea-head-btn" id="ea-close" type="button" aria-label="' + l('close') + '">' + l('close') + '</button>' +
    '</div>' +
  '</div>' +

  // Yazı boyutu
  '<div class="ea-section">' +
    '<div class="ea-section-label" id="ea-fs-label">' + l('fontSize') + '</div>' +
    '<div class="ea-font-ctrl" role="group" aria-labelledby="ea-fs-label">' +
      '<button class="ea-font-btn" id="ea-font-dec" type="button" aria-label="Yazı boyutunu küçült">A−</button>' +
      '<div class="ea-font-display" id="ea-font-val" aria-live="polite" aria-atomic="true">' + fDisp + '</div>' +
      '<button class="ea-font-btn" id="ea-font-inc" type="button" aria-label="Yazı boyutunu büyüt">A+</button>' +
    '</div>' +
  '</div>' +

  // Kontrast
  '<div class="ea-section">' +
    '<div class="ea-section-label" id="ea-contrast-label">' + l('contrast') + '</div>' +
    makeSegmented('contrast', [
      {val:'normal',   label:l('contrastNormal')},
      {val:'high',     label:l('contrastHigh')},
      {val:'negative', label:l('contrastNeg')},
    ], prefs.contrast) +
  '</div>' +

  // Renk ayarları
  '<div class="ea-section">' +
    '<div class="ea-section-label">' + l('sectionColors') + '</div>' +
    makePalette('bgColor',      BG_COLORS,      prefs.bgColor,      l('bgColor')) +
    makePalette('textColor',    TEXT_COLORS,    prefs.textColor,    l('textColor')) +
    makePalette('headingColor', HEADING_COLORS, prefs.headingColor, l('headingColor')) +
  '</div>' +

  // Metin düzeni
  '<div class="ea-section">' +
    '<div class="ea-section-label">' + l('sectionLayout') + '</div>' +
    '<div class="ea-seg-label">' + l('lineHeight') + '</div>' +
    makeSegmented('lineHeight', [
      {val:'default', label:l('lineDefault')},
      {val:'medium',  label:l('lineMedium')},
      {val:'wide',    label:l('lineWide')},
    ], prefs.lineHeight) +
    '<div class="ea-seg-label">' + l('textAlign') + '</div>' +
    makeSegmented('textAlign', [
      {val:'default', label:l('alignDefault')},
      {val:'center',  label:l('alignCenter')},
      {val:'right',   label:l('alignRight')},
      {val:'justify', label:l('alignJustify')},
    ], prefs.textAlign) +
    makeRow('highlightHeadings', prefs.highlightHeadings, l('highlightHeadings'), iconHeading()) +
  '</div>' +

  // Görünüm
  '<div class="ea-section">' +
    '<div class="ea-section-label">' + l('sectionDisplay') + '</div>' +
    makeRow('grayscale',    prefs.grayscale,    l('grayscale'), iconGrayscale()) +
    makeRow('hideImages',   prefs.hideImages,   l('hideImages'), iconHideImg()) +
    makeRow('dyslexiaFont', prefs.dyslexiaFont, l('dyslexia'),  iconFont()) +
    makeRow('textSpacing',  prefs.textSpacing,  l('spacing'),   iconSpacing()) +
  '</div>' +

  // Gezinti & Odak
  '<div class="ea-section">' +
    '<div class="ea-section-label">' + l('sectionNavigation') + '</div>' +
    makeRow('highlightLinks',  prefs.highlightLinks,  l('links'),        iconLinks()) +
    makeRow('bigCursor',       prefs.bigCursor,       l('cursor'),       iconCursor()) +
    makeRow('readingGuide',    prefs.readingGuide,    l('guide'),        iconGuide()) +
    makeRow('readingMask',     prefs.readingMask,     l('readingMask'),  iconMask()) +
    makeRow('pauseAnimations', prefs.pauseAnimations, l('animations'),   iconAnim()) +
  '</div>' +

  // Erişilebilirlik modları
  '<div class="ea-section">' +
    '<div class="ea-section-label">' + l('sectionAccessibility') + '</div>' +
    makeRow('epilepsyMode',     prefs.epilepsyMode,     l('epilepsy'),    iconEpilepsy()) +
    makeRow('visionMode',       prefs.visionMode,       l('vision'),      iconVision()) +
    makeRow('cognitiveMode',    prefs.cognitiveMode,    l('cognitive'),   iconCognitive()) +
    makeRow('adhdMode',         prefs.adhdMode,         l('adhd'),        iconAdhd()) +
    makeRow('screenReaderMode', prefs.screenReaderMode, l('screenReader'),iconScreenReader()) +
    makeRow('tts',              prefs.tts,              l('tts'),         iconTts()) +
  '</div>' +

  // AI (opsiyonel)
  (cfg.isAiEnabled ?
  '<div class="ea-section">' +
    '<div class="ea-section-label">AI</div>' +
    '<button class="ea-ai-btn" id="ea-ai-scan" type="button">' +
      '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M21 11.18V9.72c0-.47-.16-.92-.46-1.28l-2.76-3.27c-.3-.36-.74-.57-1.21-.57H7.43c-.47 0-.91.21-1.21.57L3.46 8.44C3.16 8.8 3 9.25 3 9.72v1.46c0 .55.45 1 1 1h.01L4 20c0 .55.45 1 1 1h14c.55 0 1-.45 1-1l-.01-7.82c.56 0 1.01-.45 1.01-1zM12 17c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z"/></svg>' +
      l('aiAltText') +
    '</button>' +
    '<div class="ea-ai-status" id="ea-ai-status" aria-live="polite"></div>' +
  '</div>' : '') +

  (cfg.poweredByVisible !== false ? '<div class="ea-powered">Powered by <a href="https://eascess.io" target="_blank" rel="noopener noreferrer">Eascess</a></div>' : '') +
'</div>';

        return css + html;
    }

    // ── makeRow ───────────────────────────────────────────────────────────────
    function makeRow(id, checked, label, iconSvg) {
        return '<div class="ea-row">' +
          '<label class="ea-row-label" for="ea-' + id + '">' + iconSvg + label + '</label>' +
          '<label class="ea-switch">' +
            '<input type="checkbox" id="ea-' + id + '" role="switch" aria-label="' + label + '"' + (checked ? ' checked' : '') + '>' +
            '<span class="ea-slider" aria-hidden="true"></span>' +
          '</label>' +
        '</div>';
    }

    // ── makeSegmented ─────────────────────────────────────────────────────────
    function makeSegmented(id, options, current) {
        var btns = options.map(function (opt) {
            var active = opt.val === current;
            return '<button class="ea-seg-btn' + (active ? ' active' : '') + '" data-seg="' + id + '" data-val="' + opt.val + '" aria-pressed="' + active + '">' + opt.label + '</button>';
        }).join('');
        return '<div class="ea-seg" role="group">' + btns + '</div>';
    }

    // ── makePalette ───────────────────────────────────────────────────────────
    function makePalette(id, colors, current, label) {
        var swatches = colors.map(function (c) {
            var sel = c === current && current !== '';
            return '<button class="ea-swatch' + (sel ? ' selected' : '') + '" data-palette="' + id + '" data-color="' + c + '" aria-pressed="' + sel + '" aria-label="' + c + '" style="background:' + c + '" type="button"></button>';
        }).join('');
        return '<div class="ea-palette-row">' +
          '<div class="ea-palette-label"><span>' + label + '</span>' +
            '<button class="ea-palette-reset" data-palette-reset="' + id + '" type="button">×</button>' +
          '</div>' +
          '<div class="ea-palette-swatches">' + swatches + '</div>' +
        '</div>';
    }

    // ── İkonlar ───────────────────────────────────────────────────────────────
    function iconGrayscale()    { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3a9 9 0 100 18A9 9 0 0012 3zm0 16V5a7 7 0 010 14z"/></svg>'; }
    function iconLinks()        { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z"/></svg>'; }
    function iconAnim()         { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9V8h2v8zm4 0h-2V8h2v8z"/></svg>'; }
    function iconCursor()       { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 0l16 12.3-6.6 1.5L10.1 22 4 0z"/></svg>'; }
    function iconGuide()        { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 9h18v2H3zm0 4h18v2H3z"/></svg>'; }
    function iconSpacing()      { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 7h2v10H9zm4 0h2v10h-2zM3 5v14h2V5zm16 0v14h2V5z"/></svg>'; }
    function iconFont()         { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9.93 13.5h4.14L12 7.98zM20 2H4c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-4.05 16.5l-1.14-3H9.17l-1.12 3H5.96l5.11-13h1.86l5.11 13h-2.09z"/></svg>'; }
    function iconEpilepsy()     { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2L2 7v10l10 5 10-5V7L12 2zm0 2.18L20 8.3v7.4l-8 4-8-4V8.3l8-4.12zM11 9v6h2V9h-2zm0 8v2h2v-2h-2z"/></svg>'; }
    function iconVision()       { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z"/></svg>'; }
    function iconCognitive()    { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M13 3c-4.97 0-9 4.03-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42C8.27 19.99 10.51 21 13 21c4.97 0 9-4.03 9-9s-4.03-9-9-9zm-1 5v5l4.25 2.52.77-1.28-3.52-2.09V8H12z"/></svg>'; }
    function iconAdhd()         { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 14l-5-5 1.41-1.41L12 14.17l7.59-7.59L21 8l-9 9z"/></svg>'; }
    function iconScreenReader() { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z"/></svg>'; }
    function iconHeading()      { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 4v3h5.5v12h3V7H19V4H5z"/></svg>'; }
    function iconHideImg()      { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z"/></svg>'; }
    function iconMask()         { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 3h18v3H3zm0 15h18v3H3zm0-6h18v3H3z"/></svg>'; }
    function iconTts()          { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z"/></svg>'; }

    // ── Event bağlantıları ────────────────────────────────────────────────────
    function bindEvents(shadow, cfg, prefs) {
        var toggle = shadow.getElementById('ea-toggle');
        var panel  = shadow.getElementById('ea-panel');

        function openPanel() {
            panel.classList.add('open');
            toggle.setAttribute('aria-expanded', 'true');
            var v = (cfg.position || 'bottom-right').split('-')[0];
            panel.style[v === 'bottom' ? 'bottom' : 'top'] = '80px';
            setTimeout(function () {
                var first = panel.querySelector('button, input, [tabindex="0"]');
                if (first) first.focus();
            }, 50);
        }

        function closePanel() {
            panel.classList.remove('open');
            toggle.setAttribute('aria-expanded', 'false');
            toggle.focus();
        }

        toggle.addEventListener('click', function () {
            if (panel.classList.contains('open')) {
                closePanel();
            } else {
                openPanel();
                logEvent('widget_opened');
            }
        });

        shadow.getElementById('ea-close').addEventListener('click', closePanel);

        shadow.getElementById('ea-reset').addEventListener('click', function () {
            prefs = Object.assign({}, DEFAULT_PREFS);
            savePrefs(prefs);
            applyPrefs(prefs);
            buildWidget(cfg);
        });

        // Font boyutu
        shadow.getElementById('ea-font-dec').addEventListener('click', function () {
            if (prefs.fontSize > -2) { prefs.fontSize--; updateFontDisplay(); }
        });
        shadow.getElementById('ea-font-inc').addEventListener('click', function () {
            if (prefs.fontSize < 4)  { prefs.fontSize++; updateFontDisplay(); }
        });
        function updateFontDisplay() {
            var d = shadow.getElementById('ea-font-val');
            if (d) d.textContent = prefs.fontSize === 0 ? '100%' : (100 + prefs.fontSize * 10) + '%';
            save();
        }

        // Segmented controls (contrast, lineHeight, textAlign)
        shadow.querySelectorAll('[data-seg]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var key = btn.getAttribute('data-seg');
                var val = btn.getAttribute('data-val');
                prefs[key] = val;
                shadow.querySelectorAll('[data-seg="' + key + '"]').forEach(function (b) {
                    var a = b.getAttribute('data-val') === val;
                    b.classList.toggle('active', a);
                    b.setAttribute('aria-pressed', a ? 'true' : 'false');
                });
                save(key);
            });
        });

        // Toggle switches
        var toggleKeys = [
            'grayscale','highlightLinks','pauseAnimations','bigCursor',
            'readingGuide','textSpacing','dyslexiaFont',
            'epilepsyMode','visionMode','cognitiveMode','adhdMode',
            'screenReaderMode','highlightHeadings','hideImages','readingMask','tts',
        ];

        toggleKeys.forEach(function (key) {
            var el = shadow.getElementById('ea-' + key);
            if (!el) return;
            el.addEventListener('change', function () {
                prefs[key] = el.checked;
                // Karşılıklı dışlama: readingGuide <-> readingMask
                if (key === 'readingGuide' && el.checked && prefs.readingMask) {
                    prefs.readingMask = false;
                    var m = shadow.getElementById('ea-readingMask');
                    if (m) m.checked = false;
                } else if (key === 'readingMask' && el.checked && prefs.readingGuide) {
                    prefs.readingGuide = false;
                    var g = shadow.getElementById('ea-readingGuide');
                    if (g) g.checked = false;
                }
                save(key);
            });
        });

        // Palet swatch seçimi
        shadow.querySelectorAll('[data-palette]').forEach(function (swatch) {
            swatch.addEventListener('click', function () {
                var key   = swatch.getAttribute('data-palette');
                var color = swatch.getAttribute('data-color');
                prefs[key] = color;
                shadow.querySelectorAll('[data-palette="' + key + '"]').forEach(function (s) {
                    var a = s.getAttribute('data-color') === color;
                    s.classList.toggle('selected', a);
                    s.setAttribute('aria-pressed', a ? 'true' : 'false');
                });
                save();
            });
        });

        // Palet sıfırla
        shadow.querySelectorAll('[data-palette-reset]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var key = btn.getAttribute('data-palette-reset');
                prefs[key] = '';
                shadow.querySelectorAll('[data-palette="' + key + '"]').forEach(function (s) {
                    s.classList.remove('selected');
                    s.setAttribute('aria-pressed', 'false');
                });
                save();
            });
        });

        function save(featureKey) { savePrefs(prefs); applyPrefs(prefs); if (featureKey) logEvent('feature_toggled', featureKey); }

        // Klavye: ESC + Tab trap
        panel.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') { closePanel(); return; }
            if (e.key === 'Tab') {
                var focusable = Array.from(panel.querySelectorAll(
                    'button:not([disabled]),input:not([disabled]),a[href],[tabindex]:not([tabindex="-1"])'
                ));
                if (!focusable.length) return;
                var first = focusable[0], last = focusable[focusable.length - 1];
                if (e.shiftKey) { if (shadow.activeElement === first) { e.preventDefault(); last.focus(); } }
                else            { if (shadow.activeElement === last)  { e.preventDefault(); first.focus(); } }
            }
        });

        // AI tarama
        var aiBtn = shadow.getElementById('ea-ai-scan');
        if (aiBtn) aiBtn.addEventListener('click', function () { runAiScan(shadow, cfg); });

        // Dışarı tıklayınca kapat
        document.addEventListener('click', function (e) {
            var host = document.getElementById('eascess-widget-host');
            if (host && !host.contains(e.target) && panel.classList.contains('open')) closePanel();
        });
    }

    // ── AI Alt Text Tarama ────────────────────────────────────────────────────
    function runAiScan(shadow, cfg) {
        var btn    = shadow.getElementById('ea-ai-scan');
        var status = shadow.getElementById('ea-ai-status');
        if (!btn || !status) return;

        // BUG FIX: yüklenmemiş görselleri (naturalWidth=0) doğru filtrele
        var images = Array.from(document.querySelectorAll('img')).filter(function (img) {
            var alt = img.getAttribute('alt');
            if (alt !== null && alt !== '') return false;
            if (img.complete && img.naturalWidth  > 0 && img.naturalWidth  < 50) return false;
            if (img.complete && img.naturalHeight > 0 && img.naturalHeight < 50) return false;
            var src = img.currentSrc || img.src || '';
            if (!src || src.indexOf('data:') === 0) return false;
            return true;
        });

        if (images.length === 0) { status.textContent = t(cfg, 'aiNone'); return; }

        var targets = images.slice(0, 20);
        var urls    = targets.map(function (img) { return img.currentSrc || img.src; });

        btn.disabled = true;
        status.textContent = targets.length + t(cfg, 'aiScanning');
        logEvent('ai_scan_used');

        fetch(apiUrl('/api/scan/alt-text'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ licenseKey: LICENSE_KEY, images: urls }),
        })
        .then(function (r) {
            if (!r.ok) {
                // Backend hata kodunu (QUOTA_EXCEEDED / AI_DISABLED) yakala
                return r.json().then(
                    function (err) { throw err; },
                    function () { throw {}; }
                );
            }
            return r.json();
        })
        .then(function (data) {
            var count = 0;
            (data.results || []).forEach(function (item) {
                if (!item.success || !item.altText) return;
                targets.forEach(function (img) {
                    var src = img.currentSrc || img.src;
                    if (src === item.url && (!img.getAttribute('alt') || img.getAttribute('alt') === '')) {
                        img.setAttribute('alt', item.altText);
                        // BUG FIX: Görsel altına görünür caption badge ekle
                        addCaptionBadge(img, item.altText);
                        count++;
                    }
                });
            });
            status.textContent = count + t(cfg, 'aiDone');
            btn.disabled = false;
        })
        .catch(function (err) {
            var key = 'aiError';
            if (err && err.code === 'QUOTA_EXCEEDED') key = 'aiQuota';
            else if (err && err.code === 'AI_DISABLED') key = 'aiDisabled';
            status.textContent = t(cfg, key);
            btn.disabled = false;
        });
    }

    // ── Config doğrulama (XSS koruması) ──────────────────────────────────────
    var VALID_POSITIONS = ['bottom-right', 'bottom-left', 'top-right', 'top-left'];
    var VALID_LANGUAGES  = ['tr', 'en'];
    function sanitizeConfig(cfg) {
        if (!cfg || typeof cfg !== 'object') return {};
        return {
            themeColor:       /^#[0-9a-fA-F]{6}$/.test(cfg.themeColor) ? cfg.themeColor : '#38bdf8',
            position:         VALID_POSITIONS.indexOf(cfg.position) !== -1 ? cfg.position : 'bottom-right',
            language:         VALID_LANGUAGES.indexOf(cfg.language) !== -1 ? cfg.language : 'tr',
            isAiEnabled:      cfg.isAiEnabled === true,
            poweredByVisible: cfg.poweredByVisible !== false,
            logoUrl:          typeof cfg.logoUrl === 'string'
                                  && (/^https?:\/\//.test(cfg.logoUrl) || /^\//.test(cfg.logoUrl))
                                  && !/["'<>\s\\]/.test(cfg.logoUrl) ? cfg.logoUrl : undefined,
            widgetTitle:      typeof cfg.widgetTitle === 'string' ? cfg.widgetTitle.slice(0, 30).replace(/[<>"']/g, '') : undefined,
        };
    }

    // ── postMessage (WidgetSettings canlı önizleme) ───────────────────────────
    window.addEventListener('message', function (e) {
        if (!e.data || e.data.type !== 'eascess-config-update') return;
        try { buildWidget(sanitizeConfig(e.data.config)); } catch (err) {}
    });

    // ── Başlat ────────────────────────────────────────────────────────────────
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();

})();

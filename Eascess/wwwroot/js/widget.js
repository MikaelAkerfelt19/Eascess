/**
 * Eascess Widget v1.1
 * WCAG 2.2 uyumlu erişilebilirlik katmanı
 *
 * Kullanım:
 *   <script src="https://cdn.eascess.io/widget.js"
 *           data-key="YOUR-LICENSE-KEY"
 *           data-api="https://app.eascess.io"
 *           defer></script>
 */
(function () {
    'use strict';

    // ── Kendi script tag'ini bul ──────────────────────────────────────────────
    const SELF = document.currentScript ||
        document.querySelector('script[data-key]');

    if (!SELF) return;

    const LICENSE_KEY = SELF.getAttribute('data-key');
    const API_BASE    = (SELF.getAttribute('data-api') || '').replace(/\/$/, '');
    const STORAGE_KEY = 'eascess-prefs-' + LICENSE_KEY;
    const CURRENT_DOMAIN = window.location.hostname;

    if (!LICENSE_KEY) return;

    // ── API yardımcıları ──────────────────────────────────────────────────────
    function apiUrl(path) {
        return API_BASE ? API_BASE + path : path;
    }

    // ── Bootstrapper: validate → config → render ──────────────────────────────
    function start() {
        const validateUrl = apiUrl(
            '/api/license/validate?key=' + LICENSE_KEY + '&domain=' + CURRENT_DOMAIN
        );

        fetch(validateUrl)
            .then(function (r) { return r.json(); })
            .then(function (result) {
                if (!result.valid) {
                    console.warn('[Eascess] Lisans geçersiz veya bu domain için yetkisiz.');
                    return;
                }
                fetchConfig();
            })
            .catch(function () {
                // API erişilemiyorsa (geliştirme ortamı) config'e doğrudan geç
                if (CURRENT_DOMAIN === 'localhost' || CURRENT_DOMAIN === '127.0.0.1') {
                    fetchConfig();
                } else {
                    console.warn('[Eascess] Lisans doğrulaması başarısız oldu.');
                }
            });
    }

    // ── Config çek, widget'ı başlat ───────────────────────────────────────────
    function fetchConfig() {
        fetch(apiUrl('/api/widget/config?key=' + LICENSE_KEY))
            .then(function (r) {
                if (!r.ok) throw new Error('config_failed');
                return r.json();
            })
            .then(function (cfg) { buildWidget(cfg); })
            .catch(function () {
                // Geliştirme ortamında API yoksa varsayılan config kullan
                if (CURRENT_DOMAIN === 'localhost' || CURRENT_DOMAIN === '127.0.0.1') {
                    buildWidget({ themeColor: '#38bdf8', position: 'bottom-right', language: 'tr', isAiEnabled: false });
                }
            });
    }

    // ── Tercihler (localStorage) ──────────────────────────────────────────────
    var DEFAULT_PREFS = {
        fontSize: 0,          // -2..+4
        contrast: 'normal',   // normal | high | negative
        grayscale: false,
        highlightLinks: false,
        pauseAnimations: false,
        bigCursor: false,
        readingGuide: false,
        textSpacing: false,
        dyslexiaFont: false,
    };

    function loadPrefs() {
        try { return Object.assign({}, DEFAULT_PREFS, JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}')); }
        catch (e) { return Object.assign({}, DEFAULT_PREFS); }
    }

    function savePrefs(p) {
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(p)); } catch (e) { }
    }

    // ── CSS sınıflarını document'e uygula ────────────────────────────────────
    function applyPrefs(prefs) {
        var styleEl = document.getElementById('eascess-styles');
        if (!styleEl) {
            styleEl = document.createElement('style');
            styleEl.id = 'eascess-styles';
            document.head.appendChild(styleEl);
        }

        var html = document.documentElement;
        var rules = [];

        // Font büyüklüğü
        if (prefs.fontSize !== 0) {
            var pct = 100 + prefs.fontSize * 10;
            rules.push('html { font-size: ' + pct + '% !important; }');
        }

        // Kontrast + gri tonlama
        var filters = [];
        if (prefs.contrast === 'high')     filters.push('contrast(160%)');
        if (prefs.contrast === 'negative') filters.push('invert(100%) hue-rotate(180deg)');
        if (prefs.grayscale)               filters.push('grayscale(100%)');
        if (filters.length)
            rules.push('html { filter: ' + filters.join(' ') + ' !important; }');

        // Link vurgusu
        if (prefs.highlightLinks)
            rules.push('a { outline: 2px solid #f97316 !important; outline-offset: 2px !important; background: rgba(249,115,22,.12) !important; }');

        // Animasyon durdur
        if (prefs.pauseAnimations)
            rules.push('*, *::before, *::after { animation: none !important; transition: none !important; }');

        // Büyük imleç
        html.style.cursor = prefs.bigCursor
            ? 'url("data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' width=\'32\' height=\'32\' viewBox=\'0 0 32 32\'><path fill=\'%23fff\' stroke=\'%23000\' stroke-width=\'2\' d=\'M8 2l16 16-6.5 1.5L14 28 8 2z\'/></svg>") 0 0, auto'
            : '';

        // Metin aralığı
        if (prefs.textSpacing)
            rules.push('* { letter-spacing: 0.12em !important; word-spacing: 0.16em !important; line-height: 1.8 !important; }');

        // Disleksi fontu
        if (prefs.dyslexiaFont)
            rules.push('* { font-family: Arial, "Helvetica Neue", sans-serif !important; font-weight: 500 !important; }');

        styleEl.textContent = rules.join('\n');

        // Odak halkası (klavye navigasyon vurgulayıcı)
        var focusStyleId = 'eascess-focus-styles';
        var focusStyleEl = document.getElementById(focusStyleId);
        if (!focusStyleEl) {
            focusStyleEl = document.createElement('style');
            focusStyleEl.id = focusStyleId;
            focusStyleEl.textContent = ':focus-visible { outline: 3px solid #f97316 !important; outline-offset: 3px !important; }';
            document.head.appendChild(focusStyleEl);
        }

        // Okuma rehberi
        manageReadingGuide(prefs.readingGuide);
    }

    // ── Okuma rehberi ─────────────────────────────────────────────────────────
    var guideEl = null;
    function manageReadingGuide(active) {
        if (active) {
            if (!guideEl) {
                guideEl = document.createElement('div');
                guideEl.setAttribute('aria-hidden', 'true');
                guideEl.style.cssText = [
                    'position:fixed', 'left:0', 'width:100%', 'height:36px',
                    'background:rgba(249,115,22,.25)', 'border-top:2px solid #f97316',
                    'border-bottom:2px solid #f97316', 'pointer-events:none',
                    'z-index:2147483646', 'transition:top .05s linear',
                ].join(';');
                document.body.appendChild(guideEl);
            }
            document.addEventListener('mousemove', moveGuide);
        } else {
            document.removeEventListener('mousemove', moveGuide);
            if (guideEl) { guideEl.remove(); guideEl = null; }
        }
    }

    function moveGuide(e) {
        if (guideEl) guideEl.style.top = (e.clientY - 18) + 'px';
    }

    // ── Widget inşası (Shadow DOM) ────────────────────────────────────────────
    function buildWidget(cfg) {
        // Önceki instance varsa temizle
        var existing = document.getElementById('eascess-widget-host');
        if (existing) existing.remove();

        var prefs = loadPrefs();
        applyPrefs(prefs);

        var host = document.createElement('div');
        host.id = 'eascess-widget-host';
        host.setAttribute('role', 'complementary');
        host.setAttribute('aria-label', t(cfg, 'regionLabel'));
        host.style.cssText = 'position:fixed;z-index:2147483647;';
        document.body.appendChild(host);

        // closed mode: dışarıdan JS erişimini engeller
        var shadow = host.attachShadow({ mode: 'closed' });

        var pos = cfg.position || 'bottom-right';
        var sides = pos.split('-');
        var vSide = sides[0];
        var hSide = sides[1];
        var posStyle = vSide + ':20px;' + hSide + ':20px;';

        shadow.innerHTML = buildShadowHTML(cfg, prefs, posStyle);

        bindEvents(shadow, cfg, prefs);
    }

    // ── Metinler (i18n) ───────────────────────────────────────────────────────
    var LABELS = {
        tr: {
            regionLabel: 'Erişilebilirlik Widget\'ı',
            open: 'Erişilebilirlik Menüsünü Aç',
            title: 'Erişilebilirlik',
            reset: 'Sıfırla',
            close: 'Kapat',
            fontSize: 'Yazı Boyutu',
            contrast: 'Kontrast',
            contrastNormal: 'Normal',
            contrastHigh: 'Yüksek',
            contrastNeg: 'Negatif',
            grayscale: 'Gri Tonlama',
            links: 'Bağlantıları Vurgula',
            animations: 'Animasyonları Durdur',
            cursor: 'Büyük İmleç',
            guide: 'Okuma Rehberi',
            spacing: 'Metin Aralığı',
            dyslexia: 'Okunabilir Font',
        },
        en: {
            regionLabel: 'Accessibility Widget',
            open: 'Open Accessibility Menu',
            title: 'Accessibility',
            reset: 'Reset',
            close: 'Close',
            fontSize: 'Font Size',
            contrast: 'Contrast',
            contrastNormal: 'Normal',
            contrastHigh: 'High',
            contrastNeg: 'Negative',
            grayscale: 'Grayscale',
            links: 'Highlight Links',
            animations: 'Pause Animations',
            cursor: 'Big Cursor',
            guide: 'Reading Guide',
            spacing: 'Text Spacing',
            dyslexia: 'Readable Font',
        },
    };

    function t(cfg, key) {
        var lang = ((cfg && cfg.language) || 'tr').toLowerCase();
        return (LABELS[lang] || LABELS.tr)[key] || key;
    }

    // ── Shadow DOM HTML ───────────────────────────────────────────────────────
    function buildShadowHTML(cfg, prefs, posStyle) {
        var color = cfg.themeColor || '#38bdf8';
        var l     = function (k) { return t(cfg, k); };
        var fSize = prefs.fontSize;
        var fDisp = fSize === 0 ? '100%' : (100 + fSize * 10) + '%';

        return '<style>' +
'*{box-sizing:border-box;margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}' +

// Toggle button
'#ea-toggle{' +
  'position:fixed;' + posStyle +
  'width:52px;height:52px;border-radius:50%;' +
  'background:' + color + ';' +
  'border:none;cursor:pointer;' +
  'display:flex;align-items:center;justify-content:center;' +
  'box-shadow:0 4px 24px rgba(0,0,0,.35),0 0 0 3px rgba(255,255,255,.15);' +
  'transition:transform .2s,box-shadow .2s;' +
  'z-index:2147483647;' +
'}' +
'#ea-toggle:hover{transform:scale(1.08);box-shadow:0 6px 28px rgba(0,0,0,.45),0 0 0 4px rgba(255,255,255,.2);}' +
'#ea-toggle:focus-visible{outline:3px solid #fff;outline-offset:3px;}' +
'#ea-toggle svg{width:26px;height:26px;fill:#fff;pointer-events:none}' +

// Panel
'#ea-panel{' +
  'position:fixed;' + posStyle +
  'width:320px;' +
  'background:rgba(10,10,14,.96);' +
  'backdrop-filter:blur(20px);' +
  'border:1px solid rgba(255,255,255,.1);' +
  'border-radius:18px;' +
  'box-shadow:0 20px 60px rgba(0,0,0,.6);' +
  'overflow:hidden;' +
  'transform:scale(.95) translateY(8px);' +
  'opacity:0;' +
  'pointer-events:none;' +
  'transition:transform .22s cubic-bezier(.34,1.56,.64,1),opacity .18s ease;' +
  'z-index:2147483647;' +
  'max-height:90vh;overflow-y:auto;' +
'}' +
'#ea-panel.open{transform:scale(1) translateY(0);opacity:1;pointer-events:all;}' +

// Panel header
'.ea-head{display:flex;align-items:center;justify-content:space-between;padding:1rem 1.1rem .85rem;border-bottom:1px solid rgba(255,255,255,.08);}' +
'.ea-head-title{font-size:.95rem;font-weight:700;color:#fff;}' +
'.ea-head-actions{display:flex;gap:.4rem}' +
'.ea-head-btn{' +
  'background:rgba(255,255,255,.07);border:none;border-radius:8px;' +
  'color:rgba(255,255,255,.6);font-size:.72rem;font-weight:600;' +
  'padding:.35rem .65rem;cursor:pointer;transition:background .15s,color .15s;' +
'}' +
'.ea-head-btn:hover{background:rgba(255,255,255,.12);color:#fff}' +
'.ea-head-btn:focus-visible{outline:2px solid ' + color + ';outline-offset:2px;}' +

// Sections
'.ea-section{padding:.85rem 1.1rem;border-bottom:1px solid rgba(255,255,255,.06);}' +
'.ea-section:last-child{border-bottom:none}' +
'.ea-section-label{font-size:.68rem;font-weight:600;text-transform:uppercase;letter-spacing:.6px;color:rgba(255,255,255,.35);margin-bottom:.6rem}' +

// Font size control
'.ea-font-ctrl{display:flex;align-items:center;gap:.5rem}' +
'.ea-font-btn{' +
  'width:34px;height:34px;border-radius:8px;' +
  'background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.1);' +
  'color:#fff;font-size:1rem;font-weight:700;cursor:pointer;' +
  'display:flex;align-items:center;justify-content:center;' +
  'transition:background .15s,border-color .15s;flex-shrink:0;' +
'}' +
'.ea-font-btn:hover{background:rgba(255,255,255,.14)}' +
'.ea-font-btn:focus-visible{outline:2px solid ' + color + ';outline-offset:2px;}' +
'.ea-font-display{flex:1;text-align:center;font-size:.85rem;font-weight:600;color:#fff;background:rgba(255,255,255,.05);border-radius:8px;padding:.4rem;}' +

// Segment control (contrast)
'.ea-seg{display:flex;background:rgba(255,255,255,.06);border-radius:10px;padding:3px;gap:2px}' +
'.ea-seg-btn{' +
  'flex:1;padding:.4rem;border:none;border-radius:7px;' +
  'color:rgba(255,255,255,.5);font-size:.75rem;font-weight:600;cursor:pointer;' +
  'background:transparent;transition:background .15s,color .15s;' +
'}' +
'.ea-seg-btn.active{background:' + color + ';color:#fff}' +
'.ea-seg-btn:focus-visible{outline:2px solid ' + color + ';outline-offset:2px;}' +

// Toggle rows
'.ea-row{display:flex;align-items:center;justify-content:space-between;padding:.55rem 0;}' +
'.ea-row-label{font-size:.84rem;color:rgba(255,255,255,.75);display:flex;align-items:center;gap:.5rem;cursor:pointer;}' +
'.ea-row-label svg{width:16px;height:16px;fill:rgba(255,255,255,.4);flex-shrink:0}' +

// Toggle switch
'.ea-switch{position:relative;width:40px;height:22px;flex-shrink:0}' +
'.ea-switch input{opacity:0;width:0;height:0;position:absolute}' +
'.ea-slider{position:absolute;inset:0;border-radius:22px;cursor:pointer;background:rgba(255,255,255,.12);transition:.25s;}' +
'.ea-slider::before{content:"";position:absolute;height:16px;width:16px;left:3px;bottom:3px;border-radius:50%;background:#fff;transition:.25s;}' +
'input:checked + .ea-slider{background:' + color + '}' +
'input:checked + .ea-slider::before{transform:translateX(18px)}' +
'.ea-switch input:focus-visible + .ea-slider{outline:2px solid ' + color + ';outline-offset:2px;}' +

// Powered by
'.ea-powered{text-align:center;padding:.7rem;font-size:.65rem;color:rgba(255,255,255,.2);border-top:1px solid rgba(255,255,255,.06);}' +
'.ea-powered a{color:rgba(255,255,255,.3);text-decoration:none}' +
'.ea-powered a:hover{color:rgba(255,255,255,.55)}' +

// Scrollbar
'#ea-panel::-webkit-scrollbar{width:4px}' +
'#ea-panel::-webkit-scrollbar-thumb{background:rgba(255,255,255,.1);border-radius:2px}' +
'</style>' +

// Toggle button
'<button id="ea-toggle" aria-label="' + l('open') + '" aria-expanded="false" aria-controls="ea-panel">' +
  '<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm-1-4h2v2h-2zm1.07-9.75C9.86 6.29 8 8.08 8 10h2c0-1.1.9-2 2-2s2 .9 2 2c0 2-3 1.75-3 5h2c0-2.25 3-2.5 3-5 0-2.21-1.79-4-4.93-3.75z"/></svg>' +
'</button>' +

// Accessibility panel
'<div id="ea-panel" role="dialog" aria-label="' + l('title') + '" aria-modal="true">' +

  // Header
  '<div class="ea-head">' +
    '<span class="ea-head-title">' + l('title') + '</span>' +
    '<div class="ea-head-actions">' +
      '<button class="ea-head-btn" id="ea-reset" type="button">' + l('reset') + '</button>' +
      '<button class="ea-head-btn" id="ea-close" type="button" aria-label="' + l('close') + '">' + l('close') + '</button>' +
    '</div>' +
  '</div>' +

  // Font Size
  '<div class="ea-section">' +
    '<div class="ea-section-label" id="ea-fs-label">' + l('fontSize') + '</div>' +
    '<div class="ea-font-ctrl" role="group" aria-labelledby="ea-fs-label">' +
      '<button class="ea-font-btn" id="ea-font-dec" type="button" aria-label="Yazı boyutunu küçült">A−</button>' +
      '<div class="ea-font-display" id="ea-font-val" aria-live="polite" aria-atomic="true">' + fDisp + '</div>' +
      '<button class="ea-font-btn" id="ea-font-inc" type="button" aria-label="Yazı boyutunu büyüt">A+</button>' +
    '</div>' +
  '</div>' +

  // Contrast
  '<div class="ea-section">' +
    '<div class="ea-section-label" id="ea-contrast-label">' + l('contrast') + '</div>' +
    '<div class="ea-seg" role="group" aria-labelledby="ea-contrast-label">' +
      '<button class="ea-seg-btn ' + (prefs.contrast === 'normal'   ? 'active' : '') + '" data-contrast="normal"   aria-pressed="' + (prefs.contrast === 'normal')   + '">' + l('contrastNormal') + '</button>' +
      '<button class="ea-seg-btn ' + (prefs.contrast === 'high'     ? 'active' : '') + '" data-contrast="high"     aria-pressed="' + (prefs.contrast === 'high')     + '">' + l('contrastHigh') + '</button>' +
      '<button class="ea-seg-btn ' + (prefs.contrast === 'negative' ? 'active' : '') + '" data-contrast="negative" aria-pressed="' + (prefs.contrast === 'negative') + '">' + l('contrastNeg') + '</button>' +
    '</div>' +
  '</div>' +

  // Toggles
  '<div class="ea-section">' +
    makeRow('grayscale',      prefs.grayscale,       l('grayscale'),   iconGrayscale()) +
    makeRow('highlightLinks', prefs.highlightLinks,  l('links'),       iconLinks()) +
    makeRow('pauseAnimations',prefs.pauseAnimations, l('animations'),  iconAnim()) +
    makeRow('bigCursor',      prefs.bigCursor,       l('cursor'),      iconCursor()) +
    makeRow('readingGuide',   prefs.readingGuide,    l('guide'),       iconGuide()) +
    makeRow('textSpacing',    prefs.textSpacing,     l('spacing'),     iconSpacing()) +
    makeRow('dyslexiaFont',   prefs.dyslexiaFont,    l('dyslexia'),    iconFont()) +
  '</div>' +

  '<div class="ea-powered">Powered by <a href="https://eascess.io" target="_blank" rel="noopener noreferrer">Eascess</a></div>' +
'</div>';
    }

    function makeRow(id, checked, label, iconSvg) {
        return '<div class="ea-row">' +
          '<label class="ea-row-label" for="ea-' + id + '">' + iconSvg + label + '</label>' +
          '<label class="ea-switch">' +
            '<input type="checkbox" id="ea-' + id + '" role="switch" aria-label="' + label + '" ' + (checked ? 'checked' : '') + '>' +
            '<span class="ea-slider" aria-hidden="true"></span>' +
          '</label>' +
        '</div>';
    }

    // ── İkonlar ───────────────────────────────────────────────────────────────
    function iconGrayscale() { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3a9 9 0 100 18A9 9 0 0012 3zm0 16V5a7 7 0 010 14z"/></svg>'; }
    function iconLinks()     { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z"/></svg>'; }
    function iconAnim()      { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9V8h2v8zm4 0h-2V8h2v8z"/></svg>'; }
    function iconCursor()    { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 0l16 12.3-6.6 1.5L10.1 22 4 0z"/></svg>'; }
    function iconGuide()     { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 9h18v2H3zm0 4h18v2H3z"/></svg>'; }
    function iconSpacing()   { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 7h2v10H9zm4 0h2v10h-2zM3 5v14h2V5zm16 0v14h2V5z"/></svg>'; }
    function iconFont()      { return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9.93 13.5h4.14L12 7.98zM20 2H4c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-4.05 16.5l-1.14-3H9.17l-1.12 3H5.96l5.11-13h1.86l5.11 13h-2.09z"/></svg>'; }

    // ── Event bağlantıları ────────────────────────────────────────────────────
    function bindEvents(shadow, cfg, prefs) {
        var toggle = shadow.getElementById('ea-toggle');
        var panel  = shadow.getElementById('ea-panel');

        function openPanel() {
            panel.classList.add('open');
            toggle.setAttribute('aria-expanded', 'true');
            // Panelin pozisyonuna göre toggle'dan uzaklaştır
            var pos = cfg.position || 'bottom-right';
            var v   = pos.split('-')[0];
            panel.style[v === 'bottom' ? 'bottom' : 'top'] = '80px';
            // Focus yönetimi: ilk interaktif elemente git
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

        // Aç / kapat
        toggle.addEventListener('click', function () {
            if (panel.classList.contains('open')) { closePanel(); } else { openPanel(); }
        });

        // Kapat butonu
        shadow.getElementById('ea-close').addEventListener('click', closePanel);

        // Sıfırla
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

        // Kontrast segment (aria-pressed güncelle)
        shadow.querySelectorAll('[data-contrast]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                prefs.contrast = btn.getAttribute('data-contrast');
                shadow.querySelectorAll('[data-contrast]').forEach(function (b) {
                    b.classList.remove('active');
                    b.setAttribute('aria-pressed', 'false');
                });
                btn.classList.add('active');
                btn.setAttribute('aria-pressed', 'true');
                save();
            });
        });

        // Toggle switch'ler
        ['grayscale','highlightLinks','pauseAnimations','bigCursor','readingGuide','textSpacing','dyslexiaFont'].forEach(function (key) {
            var el = shadow.getElementById('ea-' + key);
            if (el) el.addEventListener('change', function () {
                prefs[key] = el.checked;
                save();
            });
        });

        function save() {
            savePrefs(prefs);
            applyPrefs(prefs);
        }

        // Klavye: ESC → kapat, Tab sonu → toggle'a dön (focus trap)
        panel.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closePanel();
                return;
            }
            // Tab trap — panel içinde kal
            if (e.key === 'Tab') {
                var focusable = Array.from(panel.querySelectorAll(
                    'button:not([disabled]), input:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'
                ));
                if (focusable.length === 0) return;
                var first = focusable[0];
                var last  = focusable[focusable.length - 1];
                if (e.shiftKey) {
                    if (shadow.activeElement === first) { e.preventDefault(); last.focus(); }
                } else {
                    if (shadow.activeElement === last)  { e.preventDefault(); first.focus(); }
                }
            }
        });

        // Dışarı tıklayınca kapat
        document.addEventListener('click', function (e) {
            var host = document.getElementById('eascess-widget-host');
            if (host && !host.contains(e.target) && panel.classList.contains('open')) {
                closePanel();
            }
        });
    }

    // ── Başlat ────────────────────────────────────────────────────────────────
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }

})();

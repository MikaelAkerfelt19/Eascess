/**
 * Eascess Widget v1.0
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
    const STORAGE_KEY = 'eascess_prefs_' + LICENSE_KEY;

    if (!LICENSE_KEY) return;

    // ── Tercihler (localStorage) ──────────────────────────────────────────────
    const DEFAULT_PREFS = {
        fontSize: 0,        // -2..+4
        contrast: 'normal', // normal | high | negative
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
        catch { return { ...DEFAULT_PREFS }; }
    }

    function savePrefs(p) {
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(p)); } catch { }
    }

    // ── CSS sınıflarını document'e uygula ────────────────────────────────────
    const CSS_CLASS = 'eascess-active';

    function applyPrefs(prefs, root) {
        // Eski Eascess stilini temizle
        let styleEl = document.getElementById('eascess-styles');
        if (!styleEl) {
            styleEl = document.createElement('style');
            styleEl.id = 'eascess-styles';
            document.head.appendChild(styleEl);
        }

        const html = document.documentElement;
        const rules = [];

        // Font büyüklüğü
        if (prefs.fontSize !== 0) {
            const pct = 100 + prefs.fontSize * 10;
            rules.push(`html { font-size: ${pct}% !important; }`);
        }

        // Kontrast
        const filters = [];
        if (prefs.contrast === 'high')     filters.push('contrast(160%)');
        if (prefs.contrast === 'negative') filters.push('invert(100%) hue-rotate(180deg)');
        if (prefs.grayscale)               filters.push('grayscale(100%)');
        if (filters.length)
            rules.push(`html { filter: ${filters.join(' ')} !important; }`);

        // Link vurgusu
        if (prefs.highlightLinks)
            rules.push(`a { outline: 2px solid #f97316 !important; outline-offset: 2px !important; background: rgba(249,115,22,.12) !important; }`);

        // Animasyon durdur
        if (prefs.pauseAnimations)
            rules.push(`*, *::before, *::after { animation: none !important; transition: none !important; }`);

        // Büyük imleç
        html.style.cursor = prefs.bigCursor ? 'url(data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32"><path fill="%23fff" stroke="%23000" stroke-width="2" d="M8 2l16 16-6.5 1.5L14 28 8 2z"/></svg>) 0 0, auto' : '';

        // Metin aralığı
        if (prefs.textSpacing)
            rules.push(`* { letter-spacing: 0.12em !important; word-spacing: 0.16em !important; line-height: 1.8 !important; }`);

        // Disleksi fontu (system serif fallback)
        if (prefs.dyslexiaFont)
            rules.push(`* { font-family: "Arial", "Helvetica Neue", sans-serif !important; font-weight: 500 !important; }`);

        styleEl.textContent = rules.join('\n');

        // Okuma rehberi
        manageReadingGuide(prefs.readingGuide);
    }

    // ── Okuma rehberi ─────────────────────────────────────────────────────────
    let guideEl = null;
    function manageReadingGuide(active) {
        if (active) {
            if (!guideEl) {
                guideEl = document.createElement('div');
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

    // ── API'den config çek, widget'ı başlat ──────────────────────────────────
    function fetchConfig() {
        const url = API_BASE
            ? `${API_BASE}/api/widget/config?key=${LICENSE_KEY}`
            : `/api/widget/config?key=${LICENSE_KEY}`;

        fetch(url)
            .then(r => { if (!r.ok) throw new Error('invalid'); return r.json(); })
            .then(cfg => buildWidget(cfg))
            .catch(() => {
                // Geliştirme ortamı — API yoksa varsayılan config ile çalış
                if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
                    buildWidget({ themeColor: '#38bdf8', position: 'bottom-right', language: 'tr', isAiEnabled: false });
                }
            });
    }

    // ── Widget inşası (Shadow DOM) ────────────────────────────────────────────
    function buildWidget(cfg) {
        const prefs = loadPrefs();
        applyPrefs(prefs, document.documentElement);

        const host = document.createElement('div');
        host.id = 'eascess-widget-host';
        host.style.cssText = 'position:fixed;z-index:2147483647;';
        document.body.appendChild(host);

        const shadow = host.attachShadow({ mode: 'open' });

        const pos = cfg.position || 'bottom-right';
        const [vSide, hSide] = pos.split('-');
        const posStyle = `${vSide}:20px;${hSide}:20px;`;

        shadow.innerHTML = buildShadowHTML(cfg, prefs, posStyle);

        // Event bağlantıları
        bindEvents(shadow, cfg, prefs);
    }

    // ── Metinler (i18n) ───────────────────────────────────────────────────────
    const LABELS = {
        tr: {
            open: 'Erişilebilirlik Menüsü',
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
            open: 'Accessibility Menu',
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
        const lang = (cfg.language || 'tr').toLowerCase();
        return (LABELS[lang] || LABELS.tr)[key] || key;
    }

    // ── Shadow DOM HTML ───────────────────────────────────────────────────────
    function buildShadowHTML(cfg, prefs, posStyle) {
        const color  = cfg.themeColor || '#38bdf8';
        const l      = k => t(cfg, k);
        const on     = v => v ? 'on' : '';
        const fSize  = prefs.fontSize;

        return `
<style>
  *{box-sizing:border-box;margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}

  /* Floating toggle button */
  #ea-toggle{
    position:fixed;${posStyle}
    width:52px;height:52px;border-radius:50%;
    background:${color};
    border:none;cursor:pointer;
    display:flex;align-items:center;justify-content:center;
    box-shadow:0 4px 24px rgba(0,0,0,.35),0 0 0 3px rgba(255,255,255,.15);
    transition:transform .2s,box-shadow .2s;
    z-index:2147483647;
  }
  #ea-toggle:hover{transform:scale(1.08);box-shadow:0 6px 28px rgba(0,0,0,.45),0 0 0 4px rgba(255,255,255,.2);}
  #ea-toggle svg{width:26px;height:26px;fill:#fff;pointer-events:none}

  /* Panel */
  #ea-panel{
    position:fixed;${posStyle}
    width:320px;
    background:rgba(10,10,14,.96);
    backdrop-filter:blur(20px);
    border:1px solid rgba(255,255,255,.1);
    border-radius:18px;
    box-shadow:0 20px 60px rgba(0,0,0,.6);
    overflow:hidden;
    transform:scale(.95) translateY(8px);
    opacity:0;
    pointer-events:none;
    transition:transform .22s cubic-bezier(.34,1.56,.64,1),opacity .18s ease;
    z-index:2147483647;
    max-height:90vh;
    overflow-y:auto;
  }
  #ea-panel.open{
    transform:scale(1) translateY(0);
    opacity:1;
    pointer-events:all;
  }

  /* Panel header */
  .ea-head{
    display:flex;align-items:center;justify-content:space-between;
    padding:1rem 1.1rem .85rem;
    border-bottom:1px solid rgba(255,255,255,.08);
  }
  .ea-head-title{font-size:.95rem;font-weight:700;color:#fff;}
  .ea-head-actions{display:flex;gap:.4rem}
  .ea-head-btn{
    background:rgba(255,255,255,.07);border:none;border-radius:8px;
    color:rgba(255,255,255,.6);font-size:.72rem;font-weight:600;
    padding:.35rem .65rem;cursor:pointer;transition:background .15s,color .15s;
  }
  .ea-head-btn:hover{background:rgba(255,255,255,.12);color:#fff}

  /* Sections */
  .ea-section{padding:.85rem 1.1rem;border-bottom:1px solid rgba(255,255,255,.06);}
  .ea-section:last-child{border-bottom:none}
  .ea-section-label{font-size:.68rem;font-weight:600;text-transform:uppercase;letter-spacing:.6px;color:rgba(255,255,255,.35);margin-bottom:.6rem}

  /* Font size control */
  .ea-font-ctrl{display:flex;align-items:center;gap:.5rem}
  .ea-font-btn{
    width:34px;height:34px;border-radius:8px;
    background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.1);
    color:#fff;font-size:1rem;font-weight:700;cursor:pointer;
    display:flex;align-items:center;justify-content:center;
    transition:background .15s,border-color .15s;flex-shrink:0;
  }
  .ea-font-btn:hover{background:rgba(255,255,255,.14)}
  .ea-font-display{
    flex:1;text-align:center;font-size:.85rem;font-weight:600;color:#fff;
    background:rgba(255,255,255,.05);border-radius:8px;padding:.4rem;
  }

  /* Segment control (contrast) */
  .ea-seg{display:flex;background:rgba(255,255,255,.06);border-radius:10px;padding:3px;gap:2px}
  .ea-seg-btn{
    flex:1;padding:.4rem;border:none;border-radius:7px;
    color:rgba(255,255,255,.5);font-size:.75rem;font-weight:600;cursor:pointer;
    background:transparent;transition:background .15s,color .15s;
  }
  .ea-seg-btn.active{background:${color};color:#fff}

  /* Toggle rows */
  .ea-row{
    display:flex;align-items:center;justify-content:space-between;
    padding:.55rem 0;
  }
  .ea-row-label{font-size:.84rem;color:rgba(255,255,255,.75);display:flex;align-items:center;gap:.5rem}
  .ea-row-label svg{width:16px;height:16px;fill:rgba(255,255,255,.4);flex-shrink:0}

  /* Toggle switch */
  .ea-switch{position:relative;width:40px;height:22px;flex-shrink:0}
  .ea-switch input{opacity:0;width:0;height:0;position:absolute}
  .ea-slider{
    position:absolute;inset:0;border-radius:22px;cursor:pointer;
    background:rgba(255,255,255,.12);transition:.25s;
  }
  .ea-slider::before{
    content:'';position:absolute;height:16px;width:16px;
    left:3px;bottom:3px;border-radius:50%;background:#fff;transition:.25s;
  }
  input:checked + .ea-slider{background:${color}}
  input:checked + .ea-slider::before{transform:translateX(18px)}

  /* Powered by */
  .ea-powered{
    text-align:center;padding:.7rem;font-size:.65rem;color:rgba(255,255,255,.2);
    border-top:1px solid rgba(255,255,255,.06);
  }
  .ea-powered a{color:rgba(255,255,255,.3);text-decoration:none}
  .ea-powered a:hover{color:rgba(255,255,255,.55)}

  /* Scrollbar */
  #ea-panel::-webkit-scrollbar{width:4px}
  #ea-panel::-webkit-scrollbar-thumb{background:rgba(255,255,255,.1);border-radius:2px}
</style>

<!-- Toggle button -->
<button id="ea-toggle" aria-label="${l('open')}" title="${l('open')}">
  <svg viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 15v-4H7l5-8v4h4l-5 8z"/></svg>
</button>

<!-- Accessibility panel -->
<div id="ea-panel" role="dialog" aria-label="${l('title')}">

  <!-- Header -->
  <div class="ea-head">
    <span class="ea-head-title">
      <svg width="14" height="14" viewBox="0 0 24 24" style="fill:#fff;vertical-align:-2px;margin-right:5px"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 15v-4H7l5-8v4h4l-5 8z"/></svg>
      ${l('title')}
    </span>
    <div class="ea-head-actions">
      <button class="ea-head-btn" id="ea-reset">${l('reset')}</button>
      <button class="ea-head-btn" id="ea-close">${l('close')}</button>
    </div>
  </div>

  <!-- Font Size -->
  <div class="ea-section">
    <div class="ea-section-label">${l('fontSize')}</div>
    <div class="ea-font-ctrl">
      <button class="ea-font-btn" id="ea-font-dec" aria-label="-A">A−</button>
      <div class="ea-font-display" id="ea-font-val">
        ${fSize === 0 ? '100%' : (100 + fSize * 10) + '%'}
      </div>
      <button class="ea-font-btn" id="ea-font-inc" aria-label="+A">A+</button>
    </div>
  </div>

  <!-- Contrast -->
  <div class="ea-section">
    <div class="ea-section-label">${l('contrast')}</div>
    <div class="ea-seg">
      <button class="ea-seg-btn ${prefs.contrast === 'normal'   ? 'active' : ''}" data-contrast="normal">${l('contrastNormal')}</button>
      <button class="ea-seg-btn ${prefs.contrast === 'high'     ? 'active' : ''}" data-contrast="high">${l('contrastHigh')}</button>
      <button class="ea-seg-btn ${prefs.contrast === 'negative' ? 'active' : ''}" data-contrast="negative">${l('contrastNeg')}</button>
    </div>
  </div>

  <!-- Toggles -->
  <div class="ea-section">
    ${makeRow('grayscale',     prefs.grayscale,        l('grayscale'),   iconGrayscale())}
    ${makeRow('highlightLinks',prefs.highlightLinks,   l('links'),       iconLinks())}
    ${makeRow('pauseAnimations',prefs.pauseAnimations, l('animations'), iconAnim())}
    ${makeRow('bigCursor',     prefs.bigCursor,        l('cursor'),      iconCursor())}
    ${makeRow('readingGuide',  prefs.readingGuide,     l('guide'),       iconGuide())}
    ${makeRow('textSpacing',   prefs.textSpacing,      l('spacing'),     iconSpacing())}
    ${makeRow('dyslexiaFont',  prefs.dyslexiaFont,     l('dyslexia'),    iconFont())}
  </div>

  <div class="ea-powered">
    Powered by <a href="https://eascess.io" target="_blank" rel="noopener">Eascess</a>
  </div>
</div>
`;
    }

    function makeRow(id, checked, label, iconSvg) {
        return `
<div class="ea-row">
  <label class="ea-row-label" for="ea-${id}">
    ${iconSvg}${label}
  </label>
  <label class="ea-switch">
    <input type="checkbox" id="ea-${id}" ${checked ? 'checked' : ''}>
    <span class="ea-slider"></span>
  </label>
</div>`;
    }

    // ── İkonlar ───────────────────────────────────────────────────────────────
    function iconGrayscale() { return '<svg viewBox="0 0 24 24"><path d="M12 3a9 9 0 100 18A9 9 0 0012 3zm0 16V5a7 7 0 010 14z"/></svg>'; }
    function iconLinks()     { return '<svg viewBox="0 0 24 24"><path d="M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z"/></svg>'; }
    function iconAnim()      { return '<svg viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9V8h2v8zm4 0h-2V8h2v8z"/></svg>'; }
    function iconCursor()    { return '<svg viewBox="0 0 24 24"><path d="M4 0l16 12.3-6.6 1.5L10.1 22 4 0z"/></svg>'; }
    function iconGuide()     { return '<svg viewBox="0 0 24 24"><path d="M3 9h18v2H3zm0 4h18v2H3z"/></svg>'; }
    function iconSpacing()   { return '<svg viewBox="0 0 24 24"><path d="M9 7h2v10H9zm4 0h2v10h-2zM3 5v14h2V5zm16 0v14h2V5z"/></svg>'; }
    function iconFont()      { return '<svg viewBox="0 0 24 24"><path d="M9.93 13.5h4.14L12 7.98zM20 2H4c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-4.05 16.5l-1.14-3H9.17l-1.12 3H5.96l5.11-13h1.86l5.11 13h-2.09z"/></svg>'; }

    // ── Event bağlantıları ────────────────────────────────────────────────────
    function bindEvents(shadow, cfg, prefs) {
        const toggle = shadow.getElementById('ea-toggle');
        const panel  = shadow.getElementById('ea-panel');

        // Aç / kapat
        toggle.addEventListener('click', () => {
            const open = panel.classList.toggle('open');
            toggle.setAttribute('aria-expanded', open);
            if (open) {
                // Pozisyona göre panel kaymasını ayarla
                const pos = cfg.position || 'bottom-right';
                const [v] = pos.split('-');
                panel.style[v === 'bottom' ? 'bottom' : 'top'] = '80px';
            }
        });

        // Kapat butonu
        shadow.getElementById('ea-close').addEventListener('click', () => {
            panel.classList.remove('open');
            toggle.setAttribute('aria-expanded', 'false');
        });

        // Sıfırla
        shadow.getElementById('ea-reset').addEventListener('click', () => {
            prefs = { ...DEFAULT_PREFS };
            savePrefs(prefs);
            applyPrefs(prefs, document.documentElement);
            // Shadow DOM'u yenile
            const host = document.getElementById('eascess-widget-host');
            if (host) { host.remove(); }
            buildWidget(cfg);
        });

        // Font boyutu
        shadow.getElementById('ea-font-dec').addEventListener('click', () => {
            if (prefs.fontSize > -2) { prefs.fontSize--; updateFontDisplay(); save(); }
        });
        shadow.getElementById('ea-font-inc').addEventListener('click', () => {
            if (prefs.fontSize < 4)  { prefs.fontSize++; updateFontDisplay(); save(); }
        });

        function updateFontDisplay() {
            const d = shadow.getElementById('ea-font-val');
            if (d) d.textContent = prefs.fontSize === 0 ? '100%' : (100 + prefs.fontSize * 10) + '%';
            applyPrefs(prefs, document.documentElement);
        }

        // Kontrast segment
        shadow.querySelectorAll('[data-contrast]').forEach(btn => {
            btn.addEventListener('click', () => {
                prefs.contrast = btn.dataset.contrast;
                shadow.querySelectorAll('[data-contrast]').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                save();
            });
        });

        // Toggle switch'ler
        const toggleKeys = ['grayscale','highlightLinks','pauseAnimations','bigCursor','readingGuide','textSpacing','dyslexiaFont'];
        toggleKeys.forEach(key => {
            const el = shadow.getElementById('ea-' + key);
            if (el) el.addEventListener('change', () => {
                prefs[key] = el.checked;
                save();
            });
        });

        function save() {
            savePrefs(prefs);
            applyPrefs(prefs, document.documentElement);
        }

        // ESC tuşu ile kapat
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && panel.classList.contains('open')) {
                panel.classList.remove('open');
                toggle.focus();
            }
        });
    }

    // ── Başlat ────────────────────────────────────────────────────────────────
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', fetchConfig);
    } else {
        fetchConfig();
    }

})();

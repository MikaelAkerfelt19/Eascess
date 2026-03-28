/**
 * WavyBackground - Canvas tabanlı dalgalı arka plan efekti
 * React bileşeninin vanilla JS karşılığı
 * 
 * Kullanım:
 *   new WavyBackground('#my-container', { speed: 'slow', blur: 12 });
 * 
 * Veya data attribute ile:
 *   <div data-wavy-bg data-wavy-speed="fast" data-wavy-blur="10"></div>
 */
class WavyBackground {
    constructor(selector, options = {}) {
        this.container = typeof selector === 'string'
            ? document.querySelector(selector)
            : selector;

        if (!this.container) {
            console.error('WavyBackground: Container bulunamadı:', selector);
            return;
        }

        // Varsayılan ayarlar
        this.options = {
            colors: ['#38bdf8', '#818cf8', '#c084fc', '#e879f9', '#22d3ee'],
            waveWidth: 50,
            backgroundFill: 'black',
            blur: 10,
            speed: 'fast',
            waveOpacity: 0.5,
            waveCount: 5,
            ...options
        };

        // data-attribute'lardan ayarları oku
        this._readDataAttributes();

        // Durum değişkenleri
        this.animationId = null;
        this.nt = 0;
        this.noise3D = null;
        this.canvas = null;
        this.ctx = null;
        this.w = 0;
        this.h = 0;

        this._init();
    }

    _readDataAttributes() {
        const el = this.container;
        if (el.dataset.wavySpeed) this.options.speed = el.dataset.wavySpeed;
        if (el.dataset.wavyBlur) this.options.blur = parseInt(el.dataset.wavyBlur);
        if (el.dataset.wavyOpacity) this.options.waveOpacity = parseFloat(el.dataset.wavyOpacity);
        if (el.dataset.wavyBg) {
            const bg = el.dataset.wavyBgFill;
            if (bg) this.options.backgroundFill = bg;
        }
        if (el.dataset.wavyColors) {
            try {
                this.options.colors = JSON.parse(el.dataset.wavyColors);
            } catch (e) { /* varsayılan renklerle devam */ }
        }
    }

    _getSpeed() {
        switch (this.options.speed) {
            case 'slow': return 0.001;
            case 'fast': return 0.002;
            default: return 0.001;
        }
    }

    _init() {
        // simplex-noise kütüphanesi yüklü mü kontrol et
        if (typeof createNoise3D === 'undefined') {
            console.error('WavyBackground: simplex-noise kütüphanesi yüklenmemiş. CDN script ekleyin.');
            return;
        }

        this.noise3D = createNoise3D();

        // Canvas elementini oluştur
        this.canvas = document.createElement('canvas');
        this.canvas.classList.add('wavy-bg-canvas');
        this.container.insertBefore(this.canvas, this.container.firstChild);

        // Safari kontrolü
        const isSafari = typeof window !== 'undefined'
            && navigator.userAgent.includes('Safari')
            && !navigator.userAgent.includes('Chrome');

        if (isSafari) {
            this.canvas.style.filter = `blur(${this.options.blur}px)`;
        }

        this.ctx = this.canvas.getContext('2d');
        this._resize();

        // Pencere yeniden boyutlandırma
        this._resizeHandler = this._resize.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        // Animasyonu başlat
        this._render();
    }

    _resize() {
        this.w = this.canvas.width = this.container.offsetWidth;
        this.h = this.canvas.height = this.container.offsetHeight;
        this.ctx.filter = `blur(${this.options.blur}px)`;
    }

    _drawWave(n) {
        this.nt += this._getSpeed();
        for (let i = 0; i < n; i++) {
            this.ctx.beginPath();
            this.ctx.lineWidth = this.options.waveWidth;
            this.ctx.strokeStyle = this.options.colors[i % this.options.colors.length];
            for (let x = 0; x < this.w; x += 5) {
                const y = this.noise3D(x / 800, 0.3 * i, this.nt) * 100;
                this.ctx.lineTo(x, y + this.h * 0.5);
            }
            this.ctx.stroke();
            this.ctx.closePath();
        }
    }

    _render() {
        this.ctx.fillStyle = this.options.backgroundFill;
        this.ctx.globalAlpha = this.options.waveOpacity;
        this.ctx.fillRect(0, 0, this.w, this.h);
        this._drawWave(this.options.waveCount);
        this.animationId = requestAnimationFrame(this._render.bind(this));
    }

    /** Animasyonu durdur */
    destroy() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
        }
        window.removeEventListener('resize', this._resizeHandler);
        if (this.canvas && this.canvas.parentNode) {
            this.canvas.parentNode.removeChild(this.canvas);
        }
    }

    /** Ayarları güncelle (ör: renk değişikliği) */
    updateOptions(newOptions) {
        Object.assign(this.options, newOptions);
    }
}

// -------------------------------------------------------
// Otomatik başlatma: data-wavy-bg attribute'u olan
// tüm elementleri otomatik olarak WavyBackground yap
// -------------------------------------------------------
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-wavy-bg]').forEach(function (el) {
        new WavyBackground(el);
    });
});

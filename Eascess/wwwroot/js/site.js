(function () {
  "use strict";

  /* Scroll-reveal — IntersectionObserver adds .ea-visible once per element */
  function initReveal() {
    var els = document.querySelectorAll("[data-ea-reveal]");
    if (!els.length) return;

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      els.forEach(function (el) { el.style.opacity = "1"; });
      return;
    }

    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        var el = entry.target;
        el.style.animationDuration = el.dataset.eaDuration || "0.9s";
        el.style.animationDelay   = el.dataset.eaDelay    || "0s";
        el.classList.add("ea-visible");
        observer.unobserve(el);
      });
    }, { threshold: 0.12, rootMargin: "0px 0px -40px 0px" });

    els.forEach(function (el) { observer.observe(el); });
  }

  /* Glass navbar — hairline + shadow appear only after scrolling */
  function initNavbar() {
    var nav = document.querySelector(".ea-navbar");
    if (!nav) return;
    var update = function () {
      nav.classList.toggle("is-scrolled", window.scrollY > 8);
    };
    update();
    window.addEventListener("scroll", update, { passive: true });
  }

  /* Copy buttons — [data-ea-copy="#selector"] copies target textContent */
  function initCopy() {
    document.addEventListener("click", function (e) {
      var btn = e.target.closest("[data-ea-copy]");
      if (!btn) return;
      var target = document.querySelector(btn.getAttribute("data-ea-copy"));
      if (!target || !navigator.clipboard) return;
      navigator.clipboard.writeText(target.textContent.trim()).then(function () {
        var original = btn.dataset.eaCopyLabel || btn.textContent;
        btn.dataset.eaCopyLabel = original;
        btn.textContent = "Kopyalandı";
        btn.setAttribute("aria-live", "polite");
        window.setTimeout(function () { btn.textContent = original; }, 2000);
      });
    });
  }

  /* Anchor navigation — sticky navbar altında milimetrik hizalanan,
     pürüzsüz sayfa içi geçişler. Offset'i CSS scroll-padding-top uygular;
     bu fonksiyon üç boşluğu kapatır:
       1) --nav-h her zaman GERÇEK navbar yüksekliğiyle eşleşir (linkler
          dar ekranda sarıp navbar uzasa bile hizalama bozulmaz);
       2) başka sayfadan hash ile gelişte (/Home/Pricing#sss) fontlar ve
          reveal animasyonları yerleşimi kaydırır — layout oturunca hedef
          animasyonsuz yeniden hizalanır (uzun bir "crawl" animasyonu
          profesyonel hissettirmez, varıştaki konumlama anlık olmalı);
       3) sayfa içi çapa tıklamaları smooth kayar, adres çubuğu güncellenir
          ve odak hedefe taşınır (klavye/ekran okuyucu sürekliliği). */
  function initAnchorNav() {
    var nav = document.querySelector(".ea-navbar");
    var reduced = window.matchMedia("(prefers-reduced-motion: reduce)");

    function syncNavHeight() {
      if (!nav) return;
      document.documentElement.style.setProperty("--nav-h", nav.offsetHeight + "px");
    }
    syncNavHeight();
    var rt;
    window.addEventListener("resize", function () {
      window.clearTimeout(rt);
      rt = window.setTimeout(syncNavHeight, 150);
    }, { passive: true });

    function hashTarget(hash) {
      if (!hash || hash.length < 2) return null;
      try { return document.getElementById(decodeURIComponent(hash.slice(1))); }
      catch (err) { return null; }
    }

    function scrollToTarget(el, smooth) {
      /* Offset'i tarayıcı scroll-padding-top'tan (--nav-h) uygular */
      el.scrollIntoView({
        behavior: smooth && !reduced.matches ? "smooth" : "auto",
        block: "start",
      });
    }

    function focusTarget(el) {
      if (!el.hasAttribute("tabindex")) el.setAttribute("tabindex", "-1");
      el.focus({ preventScroll: true });
    }

    /* 1) Başka sayfadan hash ile geliş: layout oturdukça yeniden hizala */
    if (location.hash && hashTarget(location.hash)) {
      var realign = function () {
        var el = hashTarget(location.hash);
        if (el) { syncNavHeight(); scrollToTarget(el, false); }
      };
      requestAnimationFrame(realign);
      if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(function () { requestAnimationFrame(realign); });
      }
      window.addEventListener("load", function () { requestAnimationFrame(realign); });
    }

    /* 2) Aynı sayfa içindeki çapa tıklamaları */
    document.addEventListener("click", function (e) {
      var a = e.target.closest('a[href*="#"]');
      if (!a || a.origin !== location.origin || a.pathname !== location.pathname) return;
      var el = hashTarget(a.hash);
      if (!el) return;
      e.preventDefault();
      scrollToTarget(el, true);
      if (a.hash !== location.hash) history.pushState(null, "", a.hash);
      focusTarget(el);
    });

    /* 3) Geri/ileri gezinmesinde hash değişimi */
    window.addEventListener("hashchange", function () {
      var el = hashTarget(location.hash);
      if (el) { scrollToTarget(el, true); focusTarget(el); }
    });
  }

  function init() {
    initReveal();
    initNavbar();
    initCopy();
    initAnchorNav();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();

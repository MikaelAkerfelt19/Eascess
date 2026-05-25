(function () {
  "use strict";

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

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initReveal);
  } else {
    initReveal();
  }
})();

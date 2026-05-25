using System.Diagnostics;
using System.Net;
using Eascess.Models;
using Eascess_Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Eascess.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IPublicScanService _publicScanService;
        private readonly IMemoryCache _cache;

        public HomeController(ILogger<HomeController> logger, IPublicScanService publicScanService, IMemoryCache cache)
        {
            _logger = logger;
            _publicScanService = publicScanService;
            _cache = cache;
        }

        public IActionResult Index() => View();
        public IActionResult Privacy() => View();
        public IActionResult Wcag() => View();
        public IActionResult Pricing() => View();
        public IActionResult Docs() => View();

        [HttpGet]
        public IActionResult TestSite() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestSite(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                ModelState.AddModelError(string.Empty, "Lütfen bir URL girin.");
                return View();
            }

            // IP başına günde tarama limiti
            const int DailyScanLimit = 5;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var cacheKey = $"public-scan-{ip}-{DateTime.UtcNow:yyyy-MM-dd}";
            var count = _cache.GetOrCreate(cacheKey, e =>
            {
                e.AbsoluteExpiration = DateTime.UtcNow.Date.AddDays(1);
                return 0;
            });

            if (count >= DailyScanLimit)
            {
                ViewBag.RateLimited = true;
                return View();
            }

            _cache.Set(cacheKey, count + 1, DateTime.UtcNow.Date.AddDays(1));

            var trimmedUrl = url.Trim();
            if (!IsSafeUrl(trimmedUrl))
            {
                ModelState.AddModelError(string.Empty, "Geçersiz veya erişilemeyen URL. Lütfen genel bir web adresi girin.");
                return View();
            }

            var result = await _publicScanService.ScanAsync(trimmedUrl);
            ViewBag.ScanResult = result;
            ViewBag.ScannedUrl = trimmedUrl;
            return View();
        }

        private static bool IsSafeUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            // Dahili / özel IP aralıklarını engelle (SSRF koruması)
            var host = uri.Host;
            if (host is "localhost" or "127.0.0.1" or "::1" or "0.0.0.0")
                return false;

            if (IPAddress.TryParse(host, out var ip))
            {
                var bytes = ip.GetAddressBytes();
                // 10.x.x.x
                if (bytes[0] == 10) return false;
                // 172.16-31.x.x
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
                // 192.168.x.x
                if (bytes[0] == 192 && bytes[1] == 168) return false;
                // 169.254.x.x (link-local)
                if (bytes[0] == 169 && bytes[1] == 254) return false;
            }

            return true;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

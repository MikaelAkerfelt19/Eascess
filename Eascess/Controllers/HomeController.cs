using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Eascess.Models;
using Eascess_Application.Security;
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
            if (!await IsSafeUrlAsync(trimmedUrl))
            {
                ModelState.AddModelError(string.Empty, "Geçersiz veya erişilemeyen URL. Lütfen genel bir web adresi girin.");
                return View();
            }

            var result = await _publicScanService.ScanAsync(trimmedUrl);
            ViewBag.ScanResult = result;
            ViewBag.ScannedUrl = trimmedUrl;
            return View();
        }

        // SSRF koruması: yalnızca http/https kabul edilir; hem doğrudan IP girilen
        // hem de DNS ile özel/rezerve IP'ye çözümlenen adresler engellenir.
        private static async Task<bool> IsSafeUrlAsync(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                return false;

            // Doğrudan IP girildiyse çözümlemeye gerek yok
            if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literalIp))
                return !PrivateNetworkGuard.IsPrivateOrReserved(literalIp);

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.Host);
                return addresses.Length > 0
                    && addresses.All(a => !PrivateNetworkGuard.IsPrivateOrReserved(a));
            }
            catch (SocketException)
            {
                return false; // çözümlenemeyen adres taranmaz
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

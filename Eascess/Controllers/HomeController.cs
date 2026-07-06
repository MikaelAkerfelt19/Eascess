using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
                return !IsPrivateOrReserved(literalIp);

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.Host);
                return addresses.Length > 0 && addresses.All(a => !IsPrivateOrReserved(a));
            }
            catch (SocketException)
            {
                return false; // çözümlenemeyen adres taranmaz
            }
        }

        private static bool IsPrivateOrReserved(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            if (IPAddress.IsLoopback(ip))
                return true;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                return b[0] == 0                                    // 0.0.0.0/8
                    || b[0] == 10                                   // 10.0.0.0/8
                    || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // 100.64.0.0/10 (CGNAT)
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // 172.16.0.0/12
                    || (b[0] == 192 && b[1] == 168)                 // 192.168.0.0/16
                    || (b[0] == 169 && b[1] == 254);                // 169.254.0.0/16 (link-local)
            }

            // IPv6: link-local (fe80::/10), unique-local (fc00::/7), site-local, ::
            return ip.IsIPv6LinkLocal
                || ip.IsIPv6UniqueLocal
                || ip.IsIPv6SiteLocal
                || ip.Equals(IPAddress.IPv6Any);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

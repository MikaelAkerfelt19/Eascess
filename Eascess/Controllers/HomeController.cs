using System.Diagnostics;
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

            // IP başına günde 5 tarama
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var cacheKey = $"public-scan-{ip}-{DateTime.UtcNow:yyyy-MM-dd}";
            var count = _cache.GetOrCreate(cacheKey, e =>
            {
                e.AbsoluteExpiration = DateTime.UtcNow.Date.AddDays(1);
                return 0;
            });

            if (count >= 5)
            {
                ViewBag.RateLimited = true;
                return View();
            }

            _cache.Set(cacheKey, count + 1, DateTime.UtcNow.Date.AddDays(1));

            var result = await _publicScanService.ScanAsync(url.Trim());
            ViewBag.ScanResult = result;
            ViewBag.ScannedUrl = url.Trim();
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

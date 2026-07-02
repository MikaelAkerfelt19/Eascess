using System.Security.Cryptography;
using System.Text;
using Eascess_Application.DTOs;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;

namespace Eascess_Application.Services;

public class AiScanService : IAiScanService
{
    private readonly IRepository<Domain> _domainRepo;
    private readonly IRepository<WidgetSetting> _widgetSettingRepo;
    private readonly IRepository<ImageAltTextCache> _cacheRepo;
    private readonly IRepository<AiUsageLog> _usageLogRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAltTextGeneratorService _generator;
    private readonly IPlanService _planService;

    private const int CacheTtlDays = 30;
    private const int MaxBatchSize = 20;

    public AiScanService(
        IRepository<Domain> domainRepo,
        IRepository<WidgetSetting> widgetSettingRepo,
        IRepository<ImageAltTextCache> cacheRepo,
        IRepository<AiUsageLog> usageLogRepo,
        IUnitOfWork unitOfWork,
        IAltTextGeneratorService generator,
        IPlanService planService)
    {
        _domainRepo = domainRepo;
        _widgetSettingRepo = widgetSettingRepo;
        _cacheRepo = cacheRepo;
        _usageLogRepo = usageLogRepo;
        _unitOfWork = unitOfWork;
        _generator = generator;
        _planService = planService;
    }

    public async Task<AltTextBatchResultDto> ProcessImagesAsync(
        Guid licenseKey, List<string> imageUrls, CancellationToken ct = default)
    {
        // 1. Domain bul
        var domain = await _domainRepo.FirstOrDefaultAsync(
            d => d.LicenseKey == licenseKey && d.IsDeleted != true);

        if (domain is null)
            throw new InvalidOperationException("INVALID_LICENSE");

        // 2. AI etkin mi?
        var setting = await _widgetSettingRepo.FirstOrDefaultAsync(
            w => w.DomainId == domain.Id && w.IsActive);

        if (setting is not null && !setting.IsAiEnabled)
            throw new InvalidOperationException("AI_DISABLED");

        // 3. Kota kontrolü — kullanıcının planından dinamik olarak alınır.
        // Kota kullanıcı bazlıdır: kullanıcının TÜM domainlerindeki kullanım toplanır;
        // domain başına sayılırsa çok domainli kullanıcı kotayı domain sayısı kadar katlar.
        var plan = await _planService.GetUserActivePlanAsync(domain.UserId);
        var monthlyQuota = plan.MonthlyAiQuota;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var userDomains = await _domainRepo.FindAsync(
            d => d.UserId == domain.UserId && d.IsDeleted != true);
        var domainIds = (userDomains ?? Enumerable.Empty<Domain>()).Select(d => d.Id).ToHashSet();
        domainIds.Add(domain.Id);

        var usageLogs = await _usageLogRepo.FindAsync(
            l => domainIds.Contains(l.DomainId) && l.RequestDate >= monthStart);
        var usedThisMonth = usageLogs.Count();
        var quotaRemaining = monthlyQuota - usedThisMonth;

        if (quotaRemaining <= 0)
            throw new InvalidOperationException("QUOTA_EXCEEDED");

        // 4. Batch boyutunu sınırla
        var urls = imageUrls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct()
            .Take(MaxBatchSize)
            .ToList();

        var results = new List<AltTextResultItemDto>();
        var cachedCount = 0;
        var processedCount = 0;

        foreach (var url in urls)
        {
            if (quotaRemaining <= 0) break;

            var hash = ComputeSha256(url);

            // 5. Cache lookup
            var cached = await _cacheRepo.FirstOrDefaultAsync(
                c => c.DomainId == domain.Id && c.UrlHash == hash && c.ExpiresAt > DateTime.UtcNow);

            if (cached is not null)
            {
                results.Add(new AltTextResultItemDto
                {
                    Url = url,
                    AltText = cached.AltText,
                    Success = true,
                    FromCache = true
                });
                cachedCount++;
                continue;
            }

            // 6. Gemini'ye gönder (per-image hata yakalama — bir görsel çökerse diğerleri devam etsin)
            AltTextResult result;
            try
            {
                result = await _generator.GenerateAsync(url, ct);
            }
            catch (Exception)
            {
                result = new AltTextResult(false, null, "generator_exception", 0);
            }

            processedCount++;

            if (result.Success && !string.IsNullOrEmpty(result.AltText))
            {
                // Cache'e yaz
                await _cacheRepo.AddAsync(new ImageAltTextCache
                {
                    DomainId = domain.Id,
                    UrlHash = hash,
                    OriginalUrl = url,
                    AltText = result.AltText,
                    Provider = "Gemini",
                    GeneratedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(CacheTtlDays)
                });

                results.Add(new AltTextResultItemDto
                {
                    Url = url,
                    AltText = result.AltText,
                    Success = true,
                    FromCache = false
                });
            }
            else
            {
                results.Add(new AltTextResultItemDto
                {
                    Url = url,
                    AltText = string.Empty,
                    Success = false,
                    FromCache = false
                });
            }

            // 7. Usage log
            await _usageLogRepo.AddAsync(new AiUsageLog
            {
                DomainId = domain.Id,
                ScannedImageUrl = url,
                GeneratedAltText = result.AltText,
                TokensUsed = 0,
                RequestDate = DateTime.UtcNow
            });

            quotaRemaining--;
        }

        await _unitOfWork.SaveChangesAsync();

        return new AltTextBatchResultDto
        {
            Results = results,
            ProcessedCount = processedCount,
            CachedCount = cachedCount,
            QuotaRemaining = Math.Max(0, quotaRemaining)
        };
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input.Trim().ToLowerInvariant()));
        return Convert.ToHexStringLower(bytes);
    }
}

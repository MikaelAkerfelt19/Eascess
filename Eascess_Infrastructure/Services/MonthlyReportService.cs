using Eascess_Application.Services;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Eascess_Infrastructure.Services;

public class MonthlyReportService : IMonthlyReportService
{
    private readonly IRepository<Domain> _domainRepo;
    private readonly IRepository<ScanReport> _scanRepo;
    private readonly IRepository<ScanReportDetail> _detailRepo;
    private readonly IWidgetAnalyticsService _analytics;
    private readonly IEmailService _email;
    private readonly UserManager<AppUser> _users;
    private readonly ILogger<MonthlyReportService> _logger;

    public MonthlyReportService(
        IRepository<Domain> domainRepo,
        IRepository<ScanReport> scanRepo,
        IRepository<ScanReportDetail> detailRepo,
        IWidgetAnalyticsService analytics,
        IEmailService email,
        UserManager<AppUser> users,
        ILogger<MonthlyReportService> logger)
    {
        _domainRepo = domainRepo;
        _scanRepo   = scanRepo;
        _detailRepo = detailRepo;
        _analytics  = analytics;
        _email      = email;
        _users      = users;
        _logger     = logger;

        // QuestPDF community lisansı
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task GenerateAndSendAsync(int domainId, int year, int month, CancellationToken ct = default)
    {
        var domain = await _domainRepo.FirstOrDefaultAsync(d => d.Id == domainId && d.IsDeleted != true);
        if (domain is null) return;

        var user = await _users.FindByIdAsync(domain.UserId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return;

        // Son tarama raporu
        var reports = await _scanRepo.FindAsync(r => r.DomainId == domainId);
        var latest  = reports.OrderByDescending(r => r.ScanDate).FirstOrDefault();

        // Widget açılma — bu ay
        var monthly = await _analytics.GetMonthlyStatsAsync(domainId);
        var top     = await _analytics.GetTopFeaturesAsync(domainId, 30);

        // Kritik sorunlar
        var criticalIssues = new List<ScanReportDetail>();
        if (latest is not null)
        {
            var details = await _detailRepo.FindAsync(d => d.ScanReportId == latest.Id);
            criticalIssues = details
                .Where(d => string.Equals(d.Severity, "Critical", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();
        }

        var pdfBytes = BuildPdf(domain, user, year, month, latest, monthly, top, criticalIssues);
        var monthName = new System.Globalization.CultureInfo("tr-TR")
            .DateTimeFormat.GetMonthName(month);

        var htmlBody = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto;color:#374151">
              <h2 style="color:#0f766e">Eascess — {monthName} {year} Erişilebilirlik Raporu</h2>
              <p>Merhaba,</p>
              <p><strong>{domain.DomainUrl}</strong> sitenize ait aylık erişilebilirlik raporu hazır.</p>
              <ul>
                <li>WCAG Skoru: <strong>{latest?.WcagScore ?? 0}/100</strong></li>
                <li>Bu ay widget açılma: <strong>{monthly.ThisMonth}</strong></li>
                <li>Kritik sorun sayısı: <strong>{criticalIssues.Count}</strong></li>
              </ul>
              <p>Detaylı rapor ekte PDF olarak bulunmaktadır.</p>
              <hr style="border:none;border-top:1px solid #e5e7eb;margin:1.5rem 0">
              <p style="font-size:.8rem;color:#9ca3af">Eascess — Web Erişilebilirlik Platformu</p>
            </div>
            """;

        await _email.SendAsync(
            user.Email,
            user.UserName ?? user.Email,
            $"Eascess {monthName} {year} Raporu — {domain.DomainUrl}",
            htmlBody,
            new[] { new EmailAttachment($"eascess-rapor-{year}-{month:D2}.pdf", pdfBytes) },
            ct);

        _logger.LogInformation("[Report] Domain {Domain} için {Year}/{Month} raporu gönderildi.", domain.DomainUrl, year, month);
    }

    public async Task GenerateForAllDomainsAsync(int year, int month, CancellationToken ct = default)
    {
        var domains = await _domainRepo.FindAsync(d => d.IsDeleted != true && d.IsVerified == true);
        foreach (var domain in domains)
        {
            if (ct.IsCancellationRequested) break;
            try { await GenerateAndSendAsync(domain.Id, year, month, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Report] Domain {Id} raporu üretilemedi.", domain.Id);
            }
        }
    }

    private static byte[] BuildPdf(
        Domain domain,
        AppUser user,
        int year, int month,
        ScanReport? latest,
        MonthlyStats monthly,
        IReadOnlyList<TopFeature> top,
        IReadOnlyList<ScanReportDetail> critical)
    {
        var monthName = new System.Globalization.CultureInfo("tr-TR")
            .DateTimeFormat.GetMonthName(month);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("EASCESS")
                            .FontSize(22).Bold().FontColor(Color.FromHex("#0f766e"));
                        row.ConstantItem(120).AlignRight()
                            .Text($"{monthName} {year}").FontSize(10).FontColor(Colors.Grey.Medium);
                    });
                    col.Item().PaddingTop(4).Text(domain.DomainUrl)
                        .FontSize(13).SemiBold().FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(2).LineHorizontal(1).LineColor(Color.FromHex("#0f766e"));
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    // Özet kartlar
                    col.Item().Text("Özet").FontSize(13).Bold();
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        StatBox(row, "WCAG Skoru", $"{latest?.WcagScore ?? 0}/100");
                        StatBox(row, "Widget Açılma (Bu Ay)", monthly.ThisMonth.ToString());
                        StatBox(row, "Geçen Aya Göre", $"{(monthly.ChangePercent >= 0 ? "+" : "")}{monthly.ChangePercent}%");
                        StatBox(row, "Kritik Sorun", critical.Count.ToString());
                    });

                    col.Item().PaddingTop(20).Text("En Çok Kullanılan Özellikler").FontSize(13).Bold();
                    if (top.Any())
                    {
                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                            table.Header(h =>
                            {
                                h.Cell().Background(Color.FromHex("#f0fdf4")).Padding(6)
                                    .Text("Özellik").Bold().FontSize(10);
                                h.Cell().Background(Color.FromHex("#f0fdf4")).Padding(6)
                                    .Text("Kullanım").Bold().FontSize(10).AlignRight();
                            });
                            foreach (var f in top.Take(5))
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).Text(f.FeatureName).FontSize(10);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).Text(f.Count.ToString()).FontSize(10).AlignRight();
                            }
                        });
                    }
                    else
                    {
                        col.Item().PaddingTop(8).Text("Bu ay özellik kullanım verisi yok.")
                            .FontSize(10).FontColor(Colors.Grey.Medium);
                    }

                    if (critical.Any())
                    {
                        col.Item().PaddingTop(20).Text("Kritik Erişilebilirlik Sorunları").FontSize(13).Bold();
                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });
                            table.Header(h =>
                            {
                                h.Cell().Background(Color.FromHex("#fef2f2")).Padding(6)
                                    .Text("Kural").Bold().FontSize(10);
                                h.Cell().Background(Color.FromHex("#fef2f2")).Padding(6)
                                    .Text("Açıklama").Bold().FontSize(10);
                            });
                            foreach (var d in critical)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).Text(d.ErrorCode ?? "-").FontSize(9);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).Text((d.ErrorDescription ?? "-").Length > 120
                                        ? d.ErrorDescription!.Substring(0, 120) + "…" : d.ErrorDescription ?? "-")
                                    .FontSize(9);
                            }
                        });
                    }
                });

                page.Footer().AlignCenter()
                    .Text(t =>
                    {
                        t.Span("Eascess Erişilebilirlik Raporu — ").FontSize(9).FontColor(Colors.Grey.Medium);
                        t.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                        t.Span(" / ").FontSize(9).FontColor(Colors.Grey.Medium);
                        t.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                    });
            });
        }).GeneratePdf();
    }

    private static void StatBox(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Border(1).BorderColor(Color.FromHex("#d1fae5"))
            .Background(Color.FromHex("#f0fdf4")).Padding(10).Column(c =>
            {
                c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Medium);
                c.Item().Text(value).FontSize(16).Bold().FontColor(Color.FromHex("#0f766e"));
            });
    }
}

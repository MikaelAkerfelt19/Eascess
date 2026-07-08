using Eascess_Application.Services;
using Eascess_Domain.Constants;
using Eascess_Domain.Entities;
using Eascess_Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Eascess.Controllers;

[Authorize]
public class SupportController : Controller
{
    private readonly IRepository<SupportTicket> _ticketRepo;
    private readonly IRepository<Domain> _domainRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IPlanService _planService;
    private readonly ILogger<SupportController> _logger;

    public SupportController(
        IRepository<SupportTicket> ticketRepo,
        IRepository<Domain> domainRepo,
        IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager,
        IEmailService emailService,
        IPlanService planService,
        ILogger<SupportController> logger)
    {
        _ticketRepo = ticketRepo;
        _domainRepo = domainRepo;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _emailService = emailService;
        _planService = planService;
        _logger = logger;
    }

    // GET /Support
    public async Task<IActionResult> Index()
    {
        ViewData["Active"] = "support";
        var userId = _userManager.GetUserId(User)!;
        await SetPrioritySupportFlagAsync(userId);
        var tickets = await _ticketRepo.FindAsync(t => t.UserId == userId);
        return View(tickets.OrderByDescending(t => t.CreatedAt).ToList());
    }

    // GET /Support/Create
    [HttpGet]
    public async Task<IActionResult> Create(int? domainId)
    {
        ViewData["Active"] = "support";
        var userId = _userManager.GetUserId(User)!;
        await SetPrioritySupportFlagAsync(userId);
        await PopulateDomainSelectAsync(userId, domainId);
        return View(new SupportTicketFormModel { DomainId = domainId });
    }

    // POST /Support/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupportTicketFormModel model)
    {
        ViewData["Active"] = "support";
        var userId = _userManager.GetUserId(User)!;

        var isPriority = await SetPrioritySupportFlagAsync(userId);

        if (!ModelState.IsValid)
        {
            await PopulateDomainSelectAsync(userId, model.DomainId);
            return View(model);
        }

        var ticket = new SupportTicket
        {
            UserId = userId,
            DomainId = model.DomainId,
            Subject = model.Subject.Trim(),
            Body = model.Body.Trim(),
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow,
        };

        await _ticketRepo.AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        // E-posta bildirimi (yapılandırılmışsa)
        var user = await _userManager.GetUserAsync(User);
        if (user?.Email is not null)
        {
            var html = $"""
                <p>Merhaba {user.FullName ?? user.UserName},</p>
                <p>Destek talebiniz alındı. En kısa sürede dönüş yapacağız.</p>
                <p><strong>Konu:</strong> {System.Net.WebUtility.HtmlEncode(ticket.Subject)}</p>
                <p>Talebinizi <a href="https://app.eascess.com/Support">buradan</a> takip edebilirsiniz.</p>
                """;
            try { await _emailService.SendAsync(user.Email, user.FullName ?? user.UserName ?? "", "Destek talebiniz alındı — Eascess", html); }
            catch (Exception ex) { _logger.LogWarning(ex, "Destek talebi e-postası gönderilemedi. UserId={UserId}", user.Id); }
        }

        TempData["Success"] = isPriority
            ? "Talebiniz alındı ve öncelikli sıraya eklendi. Ekibimiz en kısa sürede yanıt verecek."
            : "Talebiniz alındı. Ekibimiz en kısa sürede yanıt verecek.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Kullanıcının planı öncelikli destek içeriyor mu (Ultra / Kurumsal).
    /// Görünümler bu bayrağa göre öncelik rozetini gösterir.
    /// </summary>
    private async Task<bool> SetPrioritySupportFlagAsync(string userId)
    {
        var plan = await _planService.GetUserActivePlanAsync(userId);
        var isPriority = plan.HasPrioritySupport;

        ViewBag.HasPrioritySupport = isPriority;
        ViewBag.PlanName = plan.Name;

        return isPriority;
    }

    private async Task PopulateDomainSelectAsync(string userId, int? selectedId)
    {
        var domains = await _domainRepo.FindAsync(d => d.UserId == userId && d.IsDeleted != true);
        ViewBag.Domains = domains
            .OrderBy(d => d.DomainUrl)
            .Select(d => new SelectListItem(d.DomainUrl, d.Id.ToString(), d.Id == selectedId))
            .ToList();
    }
}

public class SupportTicketFormModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Konu zorunludur.")]
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string Subject { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Açıklama zorunludur.")]
    [System.ComponentModel.DataAnnotations.MinLength(20, ErrorMessage = "Lütfen en az 20 karakter girin.")]
    [System.ComponentModel.DataAnnotations.MaxLength(5000, ErrorMessage = "Açıklama en fazla 5000 karakter olabilir.")]
    public string Body { get; set; } = "";

    public int? DomainId { get; set; }
}

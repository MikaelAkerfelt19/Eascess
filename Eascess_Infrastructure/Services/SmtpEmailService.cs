using Eascess_Application.Options;
using Eascess_Application.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace Eascess_Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _opt;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> opt, ILogger<SmtpEmailService> logger)
    {
        _opt    = opt.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toAddress, string toName, string subject, string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.Username))
        {
            _logger.LogWarning("[Email] SMTP kullanıcı adı yapılandırılmamış — email atlandı: {To}", toAddress);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromAddress));
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };

        if (attachments is not null)
            foreach (var att in attachments)
                builder.Attachments.Add(att.FileName, att.Data, ContentType.Parse(att.MimeType));

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_opt.Host, _opt.Port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(_opt.Username, _opt.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            _logger.LogInformation("[Email] Gönderildi: {To} — {Subject}", toAddress, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email] Gönderilemedi: {To} — {Subject}", toAddress, subject);
        }
    }
}

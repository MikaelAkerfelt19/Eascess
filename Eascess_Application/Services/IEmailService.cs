namespace Eascess_Application.Services;

public record EmailAttachment(string FileName, byte[] Data, string MimeType = "application/pdf");

public interface IEmailService
{
    Task SendAsync(string toAddress, string toName, string subject, string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken ct = default);
}

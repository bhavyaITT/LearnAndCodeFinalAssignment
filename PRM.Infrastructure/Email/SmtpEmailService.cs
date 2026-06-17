using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PRM.Application.Interfaces.Service;
using PRM.Application.Settings;

namespace PRM.Infrastructure.Email;

/// <summary>
/// Sends PRM notification emails via Gmail SMTP (smtp.gmail.com:587 + STARTTLS).
/// Uses the built-in .NET SMTP client with a Google App Password.
/// </summary>
public class SmtpEmailService(
    IOptions<EmailSettings> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            logger.LogInformation(
                "[Email disabled] To={To} Subject={Subject}",
                message.To, message.Subject);
            return;
        }

        if (!_settings.IsConfigured)
        {
            logger.LogWarning("Email enabled but GmailAddress or AppPassword is not configured.");
            return;
        }

#pragma warning disable SYSLIB0014 // SmtpClient is obsolete; used intentionally instead of MailKit.
        using var mail = new MailMessage
        {
            From = new MailAddress(_settings.GmailAddress, _settings.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(message.To, message.ToName ?? message.To));

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.SmtpUseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                _settings.GmailAddress,
                _settings.NormalizedAppPassword)
        };

        await client.SendMailAsync(mail, ct);
#pragma warning restore SYSLIB0014

        logger.LogInformation("Gmail SMTP: email sent to {To}: {Subject}", message.To, message.Subject);
    }
}

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PRM.Application.Interfaces.Service;
using PRM.Application.Settings;

namespace PRM.Infrastructure.Email;

/// <summary>
/// Sends PRM notification emails via Gmail SMTP (smtp.gmail.com:587 + STARTTLS).
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

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.FromName, _settings.GmailAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.To, message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("html") { Text = message.HtmlBody };

        using var client = new SmtpClient();
        var socketOptions = _settings.SmtpUseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.SslOnConnect;

        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions, ct);
        await client.AuthenticateAsync(_settings.GmailAddress, _settings.NormalizedAppPassword, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Gmail SMTP: email sent to {To}: {Subject}", message.To, message.Subject);
    }
}

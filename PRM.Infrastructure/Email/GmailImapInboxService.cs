using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PRM.Application.Interfaces.Service;
using PRM.Application.Settings;

namespace PRM.Infrastructure.Email;

/// <summary>
/// Reads Gmail inbox via IMAP (imap.gmail.com:993) for connection verification.
/// PRM notifications are still sent via SMTP — IMAP is only for checking incoming mail.
/// </summary>
public class GmailImapInboxService(
    IOptions<EmailSettings> options,
    ILogger<GmailImapInboxService> logger) : IEmailInboxService
{
    private readonly EmailSettings _settings = options.Value;

    public async Task<InboxStatusResult> GetInboxStatusAsync(CancellationToken ct = default)
    {
        if (!_settings.EnableInboxVerification)
            return new InboxStatusResult(false, 0, 0, null, null, "Inbox verification is disabled in EmailSettings.");

        if (!_settings.IsConfigured)
            return new InboxStatusResult(false, 0, 0, null, null, "GmailAddress and AppPassword must be configured.");

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, SecureSocketOptions.SslOnConnect, ct);
            await client.AuthenticateAsync(_settings.GmailAddress, _settings.NormalizedAppPassword, ct);

            var inbox = client.Inbox;
            await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, ct);

            string? latestSubject = null;
            string? latestFrom = null;
            if (inbox.Count > 0)
            {
                var latest = await inbox.GetMessageAsync(inbox.Count - 1, ct);
                latestSubject = latest.Subject;
                latestFrom = latest.From.ToString();
            }

            var unread = inbox.Unread;
            await client.DisconnectAsync(true, ct);

            logger.LogInformation(
                "Gmail IMAP connected. Inbox={Total}, Unread={Unread}",
                inbox.Count, unread);

            return new InboxStatusResult(true, inbox.Count, unread, latestSubject, latestFrom, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gmail IMAP connection failed");
            return new InboxStatusResult(false, 0, 0, null, null, ex.Message);
        }
    }
}

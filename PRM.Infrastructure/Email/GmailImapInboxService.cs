using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PRM.Application.Interfaces.Service;
using PRM.Application.Settings;

namespace PRM.Infrastructure.Email;

/// <summary>
/// Reads Gmail inbox via IMAP (imap.gmail.com:993 + SSL) using a Google App Password.
/// Used for admin inbox verification — PRM notifications are sent via SMTP.
/// </summary>
public class GmailImapInboxService(
    IOptions<EmailSettings> options,
    ILogger<GmailImapInboxService> logger) : IEmailInboxService
{
    private readonly EmailSettings _settings = options.Value;

    public async Task<InboxStatusResult> GetInboxStatusAsync(CancellationToken ct = default)
    {
        if (!_settings.EnableInboxVerification)
        {
            return new InboxStatusResult(
                false, 0, 0, null, null,
                "Inbox verification is disabled in EmailSettings.");
        }

        if (!_settings.IsConfigured)
        {
            return new InboxStatusResult(
                false, 0, 0, null, null,
                "GmailAddress and AppPassword must be configured.");
        }

        try
        {
            await using var session = await GmailImapSession.ConnectAndLoginAsync(
                _settings.ImapHost,
                _settings.ImapPort,
                _settings.GmailAddress,
                _settings.NormalizedAppPassword,
                ct);

            var snapshot = await session.ReadInboxSnapshotAsync(ct);

            logger.LogInformation(
                "Gmail IMAP connected. Inbox={Total}, Unread={Unread}",
                snapshot.TotalMessages,
                snapshot.UnreadMessages);

            return new InboxStatusResult(
                true,
                snapshot.TotalMessages,
                snapshot.UnreadMessages,
                snapshot.LatestSubject,
                snapshot.LatestFrom,
                null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gmail IMAP connection failed");
            return new InboxStatusResult(false, 0, 0, null, null, ex.Message);
        }
    }
}

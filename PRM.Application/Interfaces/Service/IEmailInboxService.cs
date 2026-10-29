namespace PRM.Application.Interfaces.Service;

public record InboxStatusResult(
    bool IsConnected,
    int TotalMessages,
    int UnreadMessages,
    string? LatestSubject,
    string? LatestFrom,
    string? ErrorMessage);

public interface IEmailInboxService
{
    Task<InboxStatusResult> GetInboxStatusAsync(CancellationToken ct = default);
}

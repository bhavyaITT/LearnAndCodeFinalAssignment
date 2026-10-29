namespace PRM.Application.Interfaces.Service;

public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? ToName = null);

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

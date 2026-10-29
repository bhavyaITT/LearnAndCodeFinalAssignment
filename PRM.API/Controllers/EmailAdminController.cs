using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRM.Application.Interfaces.Service;

namespace PRM.API.Controllers;

[ApiController]
[Route("api/admin/email")]
[Authorize(Roles = "Admin")]
public class EmailAdminController(IEmailService emailService, IEmailInboxService inboxService) : ControllerBase
{
    /// <summary>POST /api/admin/email/test-send?to=user@intimetec.com</summary>
    [HttpPost("test-send")]
    public async Task<IActionResult> TestSend([FromQuery] string to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to))
            return BadRequest(new { message = "Query parameter 'to' is required." });

        await emailService.SendAsync(new EmailMessage(
            to.Trim(),
            "PRM Gmail test email",
            "<html><body><h2>PRM Gmail SMTP Test</h2>" +
            "<p>If you received this message, Gmail SMTP is configured correctly in PRM.</p></body></html>",
            "PRM Test Recipient"), ct);

        return Ok(new { message = $"Test email dispatched to {to}. Check the inbox (and spam folder)." });
    }

    /// <summary>GET /api/admin/email/inbox — verify Gmail IMAP connection and unread count.</summary>
    [HttpGet("inbox")]
    public async Task<IActionResult> InboxStatus(CancellationToken ct)
    {
        var status = await inboxService.GetInboxStatusAsync(ct);
        if (!status.IsConnected)
            return BadRequest(new { message = status.ErrorMessage ?? "Could not connect to Gmail IMAP." });

        return Ok(new
        {
            status.TotalMessages,
            status.UnreadMessages,
            status.LatestFrom,
            status.LatestSubject
        });
    }
}

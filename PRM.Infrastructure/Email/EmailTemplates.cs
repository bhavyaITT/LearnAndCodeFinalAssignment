using System.Net;
using PRM.Domain.Enums;

namespace PRM.Infrastructure.Email;

internal static class EmailTemplates
{
    public static string TimesheetReminder1(string employeeName, DateOnly weekStart) =>
        Wrap("Timesheet Reminder",
            $"<p>Hi {WebUtility.HtmlEncode(employeeName)},</p>" +
            $"<p>This is a reminder that your timesheet for the week starting <strong>{weekStart:dd-MMM-yyyy}</strong> has not been submitted.</p>" +
            "<p>Please submit it as soon as possible through PRM.</p>");

    public static string TimesheetReminder2(string employeeName, DateOnly weekStart) =>
        Wrap("Final Timesheet Reminder",
            $"<p>Hi {WebUtility.HtmlEncode(employeeName)},</p>" +
            $"<p>Your timesheet for the week starting <strong>{weekStart:dd-MMM-yyyy}</strong> is still pending.</p>" +
            "<p>This is your second reminder. Failure to submit may result in your submission access being frozen.</p>");

    public static string TimesheetFrozenEmployee(string employeeName, DateOnly weekStart) =>
        Wrap("Timesheet Submission Frozen",
            $"<p>Hi {WebUtility.HtmlEncode(employeeName)},</p>" +
            $"<p>Your timesheet submission access has been frozen because the timesheet for the week starting <strong>{weekStart:dd-MMM-yyyy}</strong> was not submitted after two reminders.</p>" +
            "<p>You can still log in and view your data, but you cannot submit timesheets until your manager restores access.</p>");

    public static string TimesheetFrozenManager(string managerName, string employeeName, DateOnly weekStart) =>
        Wrap("Team Member Timesheet Frozen",
            $"<p>Hi {WebUtility.HtmlEncode(managerName)},</p>" +
            $"<p><strong>{WebUtility.HtmlEncode(employeeName)}</strong> did not submit a timesheet for the week starting <strong>{weekStart:dd-MMM-yyyy}</strong> after two reminders.</p>" +
            "<p>Their timesheet submission access has been frozen. Please review and restore access in PRM when appropriate.</p>");

    public static string ProjectAtRisk(
        string managerName,
        string projectName,
        ProjectHealth health,
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<(string Title, DateOnly DueDate, string Status, bool Overdue)> milestones,
        string? aiSummary,
        IEnumerable<(string Name, string Skills)> suggestedHelp) =>
        Wrap($"Project At Risk — {projectName}",
            $"<p>Hi {WebUtility.HtmlEncode(managerName)},</p>" +
            $"<p>The Project Health Scheduler has flagged <strong>{WebUtility.HtmlEncode(projectName)}</strong> as <span style=\"color:{HealthColor(health)};font-weight:bold\">{health}</span>.</p>" +
            $"<p><strong>Project period:</strong> {startDate:dd-MMM-yyyy} to {endDate:dd-MMM-yyyy}</p>" +
            MilestoneSection(milestones) +
            AiSection(aiSummary) +
            SuggestedHelpSection(suggestedHelp) +
            "<p>Please review the project in PRM and take action promptly.</p>");

    private static string MilestoneSection(IEnumerable<(string Title, DateOnly DueDate, string Status, bool Overdue)> milestones)
    {
        var items = milestones.ToList();
        if (items.Count == 0)
            return string.Empty;

        var rows = string.Join("", items.Select(m =>
            $"<li>{WebUtility.HtmlEncode(m.Title)} — due {m.DueDate:dd-MMM-yyyy} ({m.Status})" +
            (m.Overdue ? " <strong style=\"color:#c0392b\">OVERDUE</strong>" : "") +
            "</li>"));

        return $"<p><strong>Key milestones:</strong></p><ul>{rows}</ul>";
    }

    private static string AiSection(string? aiSummary)
    {
        if (string.IsNullOrWhiteSpace(aiSummary))
            return "<p><em>AI risk summary unavailable.</em></p>";

        return $"<p><strong>AI risk summary:</strong></p><p>{WebUtility.HtmlEncode(aiSummary)}</p>";
    }

    private static string SuggestedHelpSection(IEnumerable<(string Name, string Skills)> suggestedHelp)
    {
        var items = suggestedHelp.ToList();
        if (items.Count == 0)
            return "<p><em>No bench employees with matching skills found.</em></p>";

        var rows = string.Join("", items.Select(s =>
            $"<li>{WebUtility.HtmlEncode(s.Name)} — {WebUtility.HtmlEncode(s.Skills)}</li>"));

        return $"<p><strong>Suggested help (bench):</strong></p><ul>{rows}</ul>";
    }

    private static string HealthColor(ProjectHealth health) => health switch
    {
        ProjectHealth.OnTrack => "#27ae60",
        ProjectHealth.Attention => "#f39c12",
        ProjectHealth.AtRisk => "#c0392b",
        _ => "#333"
    };

    private static string Wrap(string title, string body) =>
        $"<html><body style=\"font-family:Segoe UI,Arial,sans-serif;color:#222;\">" +
        $"<h2 style=\"color:#1a5276;\">{WebUtility.HtmlEncode(title)}</h2>{body}" +
        "<hr/><p style=\"font-size:12px;color:#666;\">This is an automated message from PRM.</p></body></html>";
}

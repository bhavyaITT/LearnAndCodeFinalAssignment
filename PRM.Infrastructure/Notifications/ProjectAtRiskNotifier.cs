using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PRM.Application.Interfaces.Service;
using PRM.Domain.Entities;
using PRM.Domain.Enums;
using PRM.Infrastructure.Email;
using PRM.Infrastructure.Persistence;

namespace PRM.Infrastructure.Notifications;

public class ProjectAtRiskNotifier(
    PRMTDbContext context,
    IEmailService emailService,
    AI.Prompts.RiskSummaryUseCase riskSummaryUseCase,
    ILogger<ProjectAtRiskNotifier> logger)
{
    public async Task NotifyAsync(Project project, CancellationToken ct = default)
    {
        if (project.Health != ProjectHealth.AtRisk)
            return;

        if (project.Manager is null)
        {
            await context.Entry(project).Reference(p => p.Manager).Query().Include(m => m.User).LoadAsync(ct);
        }

        if (project.Manager?.User is null)
        {
            logger.LogWarning("At-risk project {ProjectId} has no manager email — skipping notification.", project.Id);
            return;
        }

        var managerUser = project.Manager.User;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!context.Entry(project).Collection(p => p.Milestones).IsLoaded)
            await context.Entry(project).Collection(p => p.Milestones).LoadAsync(ct);

        var milestoneItems = project.Milestones
            .OrderBy(m => m.DueDate)
            .Select(m => (
                m.Title,
                m.DueDate,
                m.Status.ToString(),
                m.Status != MilestoneStatus.Done && m.DueDate < today))
            .ToList();

        string? aiSummary = null;
        var riskResult = await riskSummaryUseCase.ExecuteAsync(project.Id, ct);
        if (riskResult.IsSuccess)
            aiSummary = riskResult.Data!.Summary;

        var suggestedHelp = await GetSuggestedBenchHelpAsync(ct);

        await emailService.SendAsync(new EmailMessage(
            managerUser.Email,
            $"Project at risk — {project.Name}",
            EmailTemplates.ProjectAtRisk(
                managerUser.FullName,
                project.Name,
                project.Health,
                project.StartDate,
                project.EndDate,
                milestoneItems,
                aiSummary,
                suggestedHelp),
            managerUser.FullName), ct);

        logger.LogInformation("At-risk notification sent for project {ProjectName} to {Email}",
            project.Name, managerUser.Email);
    }

    private async Task<IEnumerable<(string Name, string Skills)>> GetSuggestedBenchHelpAsync(CancellationToken ct)
    {
        var employees = await context.Employees
            .Include(e => e.User)
            .Include(e => e.Skills)
            .Where(e => e.Status == EmployeeStatus.Bench
                     && e.User.IsActive
                     && e.User.Role == UserRole.Employee)
            .OrderBy(e => e.User.FullName)
            .Take(5)
            .ToListAsync(ct);

        return employees.Select(e =>
        {
            var skills = e.Skills
                .OrderByDescending(s => s.Proficiency)
                .Take(3)
                .Select(s => s.SkillName);

            var skillText = skills.Any() ? string.Join(", ", skills) : "No skills listed";
            return (e.User.FullName, skillText);
        });
    }
}

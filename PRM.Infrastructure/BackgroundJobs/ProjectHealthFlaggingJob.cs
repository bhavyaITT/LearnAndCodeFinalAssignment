using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PRM.Application.Common;
using PRM.Domain.Entities;
using PRM.Domain.Enums;
using PRM.Infrastructure.Notifications;
using PRM.Infrastructure.Persistence;

namespace PRM.Infrastructure.BackgroundJobs;

public class ProjectHealthFlaggingJob(
    PRMTDbContext context,
    ProjectAtRiskNotifier atRiskNotifier,
    ILogger<ProjectHealthFlaggingJob> logger)
{
    public async Task ExecuteAsync()
    {
        logger.LogInformation("Starting project health flagging job at {Time}", DateTime.UtcNow);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var config = await context.SystemConfigurations.FirstOrDefaultAsync();
        var maxWeeklyHours = config?.MaxWeeklyHours ?? 40;

        var projects = await context.Projects
            .Where(p => p.Status == ProjectStatus.Active)
            .Include(p => p.Milestones)
            .Include(p => p.Manager)
                .ThenInclude(m => m.User)
            .Include(p => p.Allocations.Where(a => a.FromDate <= today && a.ToDate >= today))
                .ThenInclude(a => a.Employee)
                    .ThenInclude(e => e.Timesheets.Where(t =>
                        t.WeekStartDate == ProjectHealthCalculator.GetLastCompletedWeekMonday(today)))
                        .ThenInclude(t => t.Entries)
            .ToListAsync();

        var newlyAtRisk = new List<Project>();
        int updatedCount = 0;

        foreach (var project in projects)
        {
            var previousHealth = project.Health;
            var newHealth = ProjectHealthCalculator.ComputeHealth(project, today, maxWeeklyHours);

            if (project.Health != newHealth)
            {
                project.Health = newHealth;
                updatedCount++;
                logger.LogInformation("Project '{Name}' health changed to {Health}", project.Name, newHealth);
            }

            if (newHealth == ProjectHealth.AtRisk && previousHealth != ProjectHealth.AtRisk)
                newlyAtRisk.Add(project);
        }

        if (updatedCount > 0)
            await context.SaveChangesAsync();

        foreach (var project in newlyAtRisk)
            await atRiskNotifier.NotifyAsync(project);

        logger.LogInformation("Health flagging complete: {Count} projects updated", updatedCount);
    }
}

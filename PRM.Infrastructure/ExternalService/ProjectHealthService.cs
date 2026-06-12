using Microsoft.EntityFrameworkCore;
using PRM.Application.Common;
using PRM.Application.Interfaces.Service;
using PRM.Domain.Enums;
using PRM.Infrastructure.Persistence;

namespace PRM.Infrastructure.ExternalService;

public class ProjectHealthService(PRMTDbContext context) : IProjectHealthService
{
    public async Task RecalculateHealthAsync(int projectId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastMonday = ProjectHealthCalculator.GetLastCompletedWeekMonday(today);

        var config = await context.SystemConfigurations.FirstOrDefaultAsync(ct);
        var maxWeeklyHours = config?.MaxWeeklyHours ?? 40;

        var project = await context.Projects
            .Include(p => p.Milestones)
            .Include(p => p.Allocations.Where(a => a.FromDate <= today && a.ToDate >= today))
                .ThenInclude(a => a.Employee)
                    .ThenInclude(e => e.Timesheets.Where(t => t.WeekStartDate == lastMonday))
                        .ThenInclude(t => t.Entries)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project is null || project.Status != ProjectStatus.Active)
            return;

        project.Health = ProjectHealthCalculator.ComputeHealth(project, today, maxWeeklyHours);
        await context.SaveChangesAsync(ct);
    }
}

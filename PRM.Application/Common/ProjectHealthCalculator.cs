using PRM.Domain.Entities;
using PRM.Domain.Enums;

namespace PRM.Application.Common;

public static class ProjectHealthCalculator
{
    public static ProjectHealth ComputeHealth(
        Project project,
        DateOnly today,
        int maxWeeklyHours)
    {
        var sevenDaysFromNow = today.AddDays(7);
        var lastMonday = GetLastCompletedWeekMonday(today);
        var milestones = project.Milestones.ToList();

        var hasOverdueMilestone = milestones
            .Any(m => m.Status != MilestoneStatus.Done && m.DueDate < today);

        var activeAllocations = project.Allocations
            .Where(a => a.FromDate <= today && a.ToDate >= today)
            .ToList();

        var hasLowEffortAtRisk = activeAllocations.Any(a =>
        {
            var expected = a.UtilisationPercentage * maxWeeklyHours / 100;
            if (expected == 0) return false;

            var logged = a.Employee.Timesheets
                .FirstOrDefault(t => t.WeekStartDate == lastMonday)
                ?.Entries
                .Where(e => e.ProjectId == project.Id)
                .Sum(e => e.HoursWorked) ?? 0;

            return logged < expected * 0.5;
        });

        if (hasOverdueMilestone || hasLowEffortAtRisk)
            return ProjectHealth.AtRisk;

        var hasMilestoneAlmostDueNotStarted = milestones
            .Any(m => m.Status == MilestoneStatus.NotStarted
                   && m.DueDate >= today
                   && m.DueDate <= sevenDaysFromNow);

        var hasLowEffortAttention = activeAllocations.Any(a =>
        {
            var expected = a.UtilisationPercentage * maxWeeklyHours / 100;
            if (expected == 0) return false;

            var logged = a.Employee.Timesheets
                .FirstOrDefault(t => t.WeekStartDate == lastMonday)
                ?.Entries
                .Where(e => e.ProjectId == project.Id)
                .Sum(e => e.HoursWorked) ?? 0;

            return logged < expected * 0.75;
        });

        if (hasMilestoneAlmostDueNotStarted || hasLowEffortAttention)
            return ProjectHealth.Attention;

        return ProjectHealth.OnTrack;
    }

    public static DateOnly GetLastCompletedWeekMonday(DateOnly today)
    {
        var dayOfWeek = (int)today.DayOfWeek;
        var daysToThisMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return today.AddDays(-daysToThisMonday - 7);
    }
}

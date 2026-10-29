using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PRM.Application.Common;
using PRM.Application.Interfaces.Service;
using PRM.Domain.Entities;
using PRM.Domain.Enums;
using PRM.Infrastructure.Email;
using PRM.Infrastructure.Persistence;

namespace PRM.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily compliance workflow:
/// 1. Flag missed timesheets (Missed status record)
/// 2. Send reminder 1 on first working day after week end
/// 3. Send reminder 2 on next working day
/// 4. Freeze submission + email employee and manager after both reminders
/// </summary>
public class TimesheetComplianceJob(
    PRMTDbContext context,
    IEmailService emailService,
    ILogger<TimesheetComplianceJob> logger)
{
    public async Task ExecuteAsync()
    {
        logger.LogInformation("Starting timesheet compliance job at {Time}", DateTime.UtcNow);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastMonday = WorkingDayHelper.GetLastCompletedWeekMonday(today);
        var lastSunday = lastMonday.AddDays(6);
        var firstWorkingDayAfterWeek = lastSunday.AddDays(1);
        while (!WorkingDayHelper.IsWorkingDay(firstWorkingDayAfterWeek))
            firstWorkingDayAfterWeek = firstWorkingDayAfterWeek.AddDays(1);

        //var workingDaysSinceDeadline = WorkingDayHelper.CountWorkingDays(firstWorkingDayAfterWeek, today);
        var workingDaysSinceDeadline = 3;
        if (workingDaysSinceDeadline <= 0)
        {
            logger.LogInformation("No working days elapsed since last completed week — skipping reminders.");
            return;
        }

        await FlagMissedTimesheetsAsync(lastMonday, lastSunday);

        var allocatedEmployeeIds = await context.Allocations
            .Where(a => a.FromDate <= lastSunday && a.ToDate >= lastMonday)
            .Select(a => a.EmployeeId)
            .Distinct()
            .ToListAsync();

        var submittedEmployeeIds = await context.Timesheets
            .Where(t => t.WeekStartDate == lastMonday
                     && t.Status == TimesheetStatus.Submitted
                     && allocatedEmployeeIds.Contains(t.EmployeeId))
            .Select(t => t.EmployeeId)
            .ToListAsync();

        var pendingEmployeeIds = allocatedEmployeeIds.Except(submittedEmployeeIds).ToList();
        if (pendingEmployeeIds.Count == 0)
        {
            logger.LogInformation("All allocated employees submitted for week {Week}", lastMonday);
            return;
        }

        foreach (var employeeId in pendingEmployeeIds)
        {
            var employee = await context.Employees
                .Include(e => e.User)
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee?.User is null || !employee.User.IsActive)
                continue;

            var user = employee.User;

            if (user.PendingMissedWeekStart != lastMonday)
            {
                user.PendingMissedWeekStart = lastMonday;
                user.TimesheetReminderCount = 0;
            }

            if (workingDaysSinceDeadline >= 1 && user.TimesheetReminderCount < 1)
            {
                await emailService.SendAsync(new EmailMessage(
                    user.Email,
                    $"Timesheet reminder — week of {lastMonday:dd-MMM-yyyy}",
                    EmailTemplates.TimesheetReminder1(user.FullName, lastMonday),
                    user.FullName));

                user.TimesheetReminderCount = 1;
                logger.LogInformation("Reminder 1 sent to employee {Id}", employeeId);
            }
            else if (workingDaysSinceDeadline >= 2 && user.TimesheetReminderCount < 2)
            {
                await emailService.SendAsync(new EmailMessage(
                    user.Email,
                    $"Final timesheet reminder — week of {lastMonday:dd-MMM-yyyy}",
                    EmailTemplates.TimesheetReminder2(user.FullName, lastMonday),
                    user.FullName));

                user.TimesheetReminderCount = 2;
                logger.LogInformation("Reminder 2 sent to employee {Id}", employeeId);
            }
            else if (workingDaysSinceDeadline >= 3 && !user.TimesheetSubmissionFrozen)
            {
                user.TimesheetSubmissionFrozen = true;

                await emailService.SendAsync(new EmailMessage(
                    user.Email,
                    "Timesheet submission access frozen",
                    EmailTemplates.TimesheetFrozenEmployee(user.FullName, lastMonday),
                    user.FullName));

                if (employee.Manager is not null)
                {
                    await emailService.SendAsync(new EmailMessage(
                        employee.Manager.Email,
                        $"Team member timesheet frozen — {user.FullName}",
                        EmailTemplates.TimesheetFrozenManager(
                            employee.Manager.FullName, user.FullName, lastMonday),
                        employee.Manager.FullName));
                }

                logger.LogWarning("Timesheet submission frozen for employee {Id}", employeeId);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Timesheet compliance job complete.");
    }

    private async Task FlagMissedTimesheetsAsync(DateOnly lastMonday, DateOnly lastSunday)
    {
        var allocatedEmployeeIds = await context.Allocations
            .Where(a => a.FromDate <= lastSunday && a.ToDate >= lastMonday)
            .Select(a => a.EmployeeId)
            .Distinct()
            .ToListAsync();

        var submittedEmployeeIds = await context.Timesheets
            .Where(t => t.WeekStartDate == lastMonday
                     && t.Status == TimesheetStatus.Submitted
                     && allocatedEmployeeIds.Contains(t.EmployeeId))
            .Select(t => t.EmployeeId)
            .ToListAsync();

        foreach (var employeeId in allocatedEmployeeIds.Except(submittedEmployeeIds))
        {
            var exists = await context.Timesheets.AnyAsync(t =>
                t.EmployeeId == employeeId && t.WeekStartDate == lastMonday);

            if (exists)
                continue;

            context.Timesheets.Add(new Timesheet
            {
                EmployeeId = employeeId,
                WeekStartDate = lastMonday,
                TotalHours = 0,
                Status = TimesheetStatus.Missed
            });
        }

        await context.SaveChangesAsync();
    }
}

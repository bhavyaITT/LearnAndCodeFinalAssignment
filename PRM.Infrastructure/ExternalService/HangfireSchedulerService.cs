using PRM.Application.Interfaces.Service;
using PRM.Infrastructure.BackgroundJobs;
using Hangfire;

namespace PRM.Infrastructure.ExternalService;

public class HangfireSchedulerService(
    IRecurringJobManager recurringJobManager,
    IBackgroundJobClient backgroundJobClient) : ISchedulerService
{
    private const string UtilisationJobId = "prm-utilisation-recompute";
    private const string HealthJobId = "prm-project-health-flagging";
    private const string TimesheetJobId = "prm-timesheet-compliance";

    public void ScheduleRecurringJobs(int intervalHours)
    {
        var cron = intervalHours switch
        {
            1 => Cron.Hourly(),
            24 => Cron.Daily(),
            _ => $"0 */{intervalHours} * * *"
        };

        recurringJobManager.AddOrUpdate<UtilisationRecomputeJob>(
            UtilisationJobId,
            job => job.ExecuteAsync(),
            cron);

        recurringJobManager.AddOrUpdate<ProjectHealthFlaggingJob>(
            HealthJobId,
            job => job.ExecuteAsync(),
            cron);

        // Timesheet compliance: detect, remind, freeze — daily 9 AM UTC
        recurringJobManager.AddOrUpdate<TimesheetComplianceJob>(
            TimesheetJobId,
            job => job.ExecuteAsync(),
            "0 9 * * *");
    }

    public void TriggerUtilisationRecompute() =>
        backgroundJobClient.Enqueue<UtilisationRecomputeJob>(job => job.ExecuteAsync());

    public void TriggerHealthFlagging() =>
        backgroundJobClient.Enqueue<ProjectHealthFlaggingJob>(job => job.ExecuteAsync());

    public void TriggerMissedTimesheetDetection() =>
        backgroundJobClient.Enqueue<TimesheetComplianceJob>(job => job.ExecuteAsync());
}

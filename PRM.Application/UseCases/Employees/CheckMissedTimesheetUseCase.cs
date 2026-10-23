using PRM.Application.Common;
using PRM.Application.DTOs.Employee;
using PRM.Application.Interfaces.Repository;

namespace PRM.Application.UseCases.Employees;

public class CheckMissedTimesheetUseCase(IUnitOfWork unitOfWork)
{
    public async Task<Result<MissedTimesheetReminderResponse>> ExecuteAsync(
        int employeeId, CancellationToken ct = default)
    {
        var user = await unitOfWork.Users.GetByIdAsync(employeeId, ct);
        if (user?.TimesheetSubmissionFrozen == true)
        {
            return Result<MissedTimesheetReminderResponse>.Success(
                new MissedTimesheetReminderResponse(
                    IsMissing: true,
                    MissingWeekStart: user.PendingMissedWeekStart,
                    IsSubmissionFrozen: true));
        }

        var lastMonday = WorkingDayHelper.GetLastCompletedWeekMonday(DateOnly.FromDateTime(DateTime.UtcNow));
        var lastSunday = lastMonday.AddDays(6);

        var hadAllocations = await unitOfWork.Allocations.AnyAsync(
            a => a.EmployeeId == employeeId
              && a.FromDate <= lastSunday
              && a.ToDate >= lastMonday,
            ct);

        if (!hadAllocations)
            return Result<MissedTimesheetReminderResponse>.Success(
                new MissedTimesheetReminderResponse(IsMissing: false, MissingWeekStart: null));

        var hasSubmitted = await unitOfWork.Timesheets.AnyAsync(
            t => t.EmployeeId == employeeId
              && t.WeekStartDate == lastMonday
              && t.Status == Domain.Enums.TimesheetStatus.Submitted,
            ct);

        return Result<MissedTimesheetReminderResponse>.Success(
            new MissedTimesheetReminderResponse(
                IsMissing: !hasSubmitted,
                MissingWeekStart: !hasSubmitted ? lastMonday : null));
    }
}

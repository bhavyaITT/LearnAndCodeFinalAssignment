using PRM.Application.Interfaces.Repository;
using PRM.Application.Interfaces.Service;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Manager;

public class RestoreTimesheetSubmissionUseCase(IUnitOfWork unitOfWork, IQueryService queryService)
{
    public async Task<Result> ExecuteAsync(int employeeId, int managerEmployeeId, CancellationToken ct = default)
    {
        if (!await queryService.IsEmployeeUnderManagerAsync(employeeId, managerEmployeeId, ct))
            return Result.Failure("Employee is not on your team.");

        var employee = await unitOfWork.Employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return Result.Failure($"Employee with ID {employeeId} not found.");

        var user = await unitOfWork.Users.GetByIdAsync(employeeId, ct);
        if (user is null)
            return Result.Failure("Employee user record not found.");

        if (!user.TimesheetSubmissionFrozen)
            return Result.Failure("This employee's timesheet submission is not frozen.");

        user.TimesheetSubmissionFrozen = false;
        user.TimesheetReminderCount = 0;

        if (user.PendingMissedWeekStart.HasValue)
        {
            var missedWeek = user.PendingMissedWeekStart.Value;
            var missedTimesheet = await unitOfWork.Timesheets.FirstOrDefaultAsync(
                t => t.EmployeeId == employeeId
                  && t.WeekStartDate == missedWeek
                  && t.Status == TimesheetStatus.Missed, ct);

            if (missedTimesheet is not null)
                unitOfWork.Timesheets.Remove(missedTimesheet);

            user.PendingMissedWeekStart = null;
        }

        unitOfWork.Users.Update(user);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

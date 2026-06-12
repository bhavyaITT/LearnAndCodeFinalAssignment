using PRM.Application.Common;
using PRM.Application.DTOs.Employee;
using PRM.Application.Interfaces.Repository;
using PRM.Application.Interfaces.Service;
using PRM.Domain.Enums;
namespace PRM.Application.UseCases.Employees
{
    /// <summary>
    /// Returns the projects an employee can log hours for in a given week,
    /// together with their per-project hour cap.
    /// This powers the Submit Timesheet screen's project list.
    /// </summary>
    public class GetActiveAllocationsForWeekUseCase(
        IUnitOfWork unitOfWork,
        IQueryService queryService)
    {
        public async Task<Result<ActiveAllocationsForWeekResponse>> ExecuteAsync(
            int employeeId,
            DateOnly weekStart,
            CancellationToken ct = default)
        {
            var monday = ValidationRules.ToMonday(weekStart);

            var user = await unitOfWork.Users.GetByIdAsync(employeeId, ct);
            if (user?.TimesheetSubmissionFrozen == true)
                return Result<ActiveAllocationsForWeekResponse>.Failure(
                    "Your timesheet submission access is frozen. Please contact your manager.");

            var weekError = ValidationRules.ValidateTimesheetWeek(weekStart);
            if (weekError is not null)
                return Result<ActiveAllocationsForWeekResponse>.Failure(weekError);

            var weekEnd = ValidationRules.WeekEnd(monday);

            var existingTimesheet = await unitOfWork.Timesheets
                .FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStartDate == monday, ct);

            if (existingTimesheet?.Status == TimesheetStatus.Missed)
                return Result<ActiveAllocationsForWeekResponse>.Failure(
                    $"Timesheet for the week starting {monday:dd-MM-yyyy} was marked as missed.");

            var hadAllocationInWeek = await unitOfWork.Allocations.AnyAsync(
                a => a.EmployeeId == employeeId
                     && a.FromDate <= weekEnd
                     && a.ToDate >= monday,
                ct);

            if (!hadAllocationInWeek)
                return Result<ActiveAllocationsForWeekResponse>.Failure(
                    "You cannot submit a timesheet for a week outside your allocation period.");

            // Fetch system max weekly hours — drives per-project cap calculation
            var config = (await unitOfWork.SystemConfigurations.GetAllAsync(ct)).FirstOrDefault();
            var maxWeeklyHours = config?.MaxWeeklyHours ?? 40;

            var allocations = await queryService.GetActiveAllocationsForWeekAsync(
                employeeId, monday, maxWeeklyHours, ct);

            return Result<ActiveAllocationsForWeekResponse>.Success(new ActiveAllocationsForWeekResponse(
                WeekStartDate: monday,
                MaxWeeklyHours: maxWeeklyHours,
                Allocations: allocations
            ));
        }
    }
}

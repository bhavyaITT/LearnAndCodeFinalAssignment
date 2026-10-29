using PRM.Application.DTOs.Manager;
using PRM.Application.Interfaces.Repository;
using PRM.Application.Common;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Manager;

public class EndAllocationUseCase(IUnitOfWork unitOfWork)
{
    public async Task<Result<EndAllocationResponse>> ExecuteAsync(
        int allocationId,
        int requestingManagerEmployeeId,
        CancellationToken ct = default)
    {
        var allocation = await unitOfWork.Allocations.GetByIdAsync(allocationId, ct);
        if (allocation is null)
            return Result<EndAllocationResponse>.Failure($"Allocation with ID {allocationId} not found.");

        var project = await unitOfWork.Projects.GetByIdAsync(allocation.ProjectId, ct);
        if (project is null || project.ManagerId != requestingManagerEmployeeId)
            return Result<EndAllocationResponse>.Failure("You can only end allocations on your own projects.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (allocation.ToDate < today)
            return Result<EndAllocationResponse>.Failure("This allocation has already ended.");

        var employeeId = allocation.EmployeeId;

        // Ending effective today frees the employee immediately:
        // - future or same-day start → remove the allocation entirely
        // - otherwise → last active day is yesterday
        var lastActiveDay = today.AddDays(-1);
        var wasRemoved = today <= allocation.FromDate || lastActiveDay < allocation.FromDate;

        if (wasRemoved)
            unitOfWork.Allocations.Remove(allocation);
        else
        {
            allocation.ToDate = lastActiveDay;
            unitOfWork.Allocations.Update(allocation);
        }

        await unitOfWork.SaveChangesAsync(ct);

        var employee = await unitOfWork.Employees.GetByIdAsync(employeeId, ct);
        if (employee is not null)
        {
            var hasActiveToday = await HasActiveAllocationTodayAsync(employeeId, today, ct);

            employee.Status = hasActiveToday
                ? EmployeeStatus.Allocated
                : EmployeeStatus.Bench;

            unitOfWork.Employees.Update(employee);
            await unitOfWork.SaveChangesAsync(ct);
        }

        var employeeUser = employee is not null
            ? await unitOfWork.Users.GetByIdAsync(employee.Id, ct)
            : null;

        return Result<EndAllocationResponse>.Success(new EndAllocationResponse(
            AllocationId: allocationId,
            EmployeeName: employeeUser?.FullName ?? "Unknown",
            ProjectName: project.Name,
            EndedOnDate: today,
            EmployeeNewStatus: employee?.Status.ToString() ?? "Unknown",
            WasRemoved: wasRemoved
        ));
    }

    private async Task<bool> HasActiveAllocationTodayAsync(
        int employeeId, DateOnly today, CancellationToken ct)
    {
        var allocations = await unitOfWork.Allocations.FindAsync(
            a => a.EmployeeId == employeeId, ct);

        return allocations.Any(a => AllocationDateHelper.IsActiveOnDate(a, today));
    }
}

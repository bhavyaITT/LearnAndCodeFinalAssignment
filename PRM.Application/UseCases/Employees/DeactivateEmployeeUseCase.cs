using PRM.Application.Interfaces.Repository;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Employees;

/// <summary>
/// Deactivation rules per BRD:
/// 1. Active allocations are ended immediately when employee is Allocated (ToDate = today)
/// 2. The linked user account is blocked (IsActive = false)
/// 3. All historical data (timesheets, past allocations) is preserved
/// </summary>
public class DeactivateEmployeeUseCase(IUnitOfWork unitOfWork)
{
    public async Task<Result> ExecuteAsync(int employeeId, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return Result.Failure($"Employee with ID {employeeId} not found.");

        var user = await unitOfWork.Users.GetByIdAsync(employeeId, ct);
        if (user is null)
            return Result.Failure($"User account for employee ID {employeeId} not found.");

        if (user.Role != UserRole.Employee && user.Role != UserRole.Manager)
            return Result.Failure("Only users with Employee or Manager roles can be deactivated here.");

        if (!user.IsActive)
            return Result.Failure("Employee is already deactivated.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (employee.Status != EmployeeStatus.Bench)
        {
            var activeAllocations = (await unitOfWork.Allocations
                .FindAsync(a => a.EmployeeId == employeeId && a.ToDate >= today, ct))
                .ToList();

            foreach (var allocation in activeAllocations)
            {
                allocation.ToDate = today;
                unitOfWork.Allocations.Update(allocation);
            }
        }

        user.IsActive = false;
        unitOfWork.Users.Update(user);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

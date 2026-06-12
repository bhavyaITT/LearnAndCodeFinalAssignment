using PRM.Application.Interfaces.Repository;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Employees;

public class AssignManagerUseCase(IUnitOfWork unitOfWork)
{
    public async Task<Result> ExecuteAsync(int employeeId, int managerId, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return Result.Failure($"Employee with ID {employeeId} not found.");

        var employeeUser = await unitOfWork.Users.GetByIdAsync(employeeId, ct);
        if (employeeUser is null)
            return Result.Failure($"User account for employee ID {employeeId} not found.");

        if (employeeUser.Role != UserRole.Employee)
            return Result.Failure("A manager can only be assigned to a user with the Employee role.");

        var manager = await unitOfWork.Employees.GetByIdAsync(managerId, ct);
        if (manager is null)
            return Result.Failure($"Manager with ID {managerId} not found.");

        var managerUser = await unitOfWork.Users.GetByIdAsync(managerId, ct);
        if (managerUser is null)
            return Result.Failure($"User account for manager ID {managerId} not found.");

        if (managerUser.Role != UserRole.Manager)
            return Result.Failure("The selected manager must have the Manager role.");

        if (employeeId == managerId)
            return Result.Failure("An employee cannot be assigned as their own manager.");

        employee.ManagerId = managerId;
        unitOfWork.Employees.Update(employee);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

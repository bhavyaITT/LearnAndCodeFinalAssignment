using PRM.Application.Common;
using PRM.Application.DTOs.Employee;
using PRM.Application.Interfaces.Repository;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Employees;

public class UpdateEmployeeUseCase(IUnitOfWork unitOfWork)
{
    public async Task<Result<EmployeeSummaryResponse>> ExecuteAsync(
        int employeeId, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await unitOfWork.Employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return Result<EmployeeSummaryResponse>.Failure($"Employee with ID {employeeId} not found.");

        var user = await unitOfWork.Users.GetByIdAsync(employeeId, ct);
        if (user is null)
            return Result<EmployeeSummaryResponse>.Failure($"User account for employee ID {employeeId} not found.");

        if (user.Role != UserRole.Employee && user.Role != UserRole.Manager)
            return Result<EmployeeSummaryResponse>.Failure(
                "Only users with Employee or Manager roles can be updated here.");

        if (!user.IsActive)
            return Result<EmployeeSummaryResponse>.Failure("Cannot update a deactivated employee.");

        if (!ValidationRules.IsIntimetecEmail(request.Email))
            return Result<EmployeeSummaryResponse>.Failure(
                "Email must be in the format user@intimetec.com");

        var departmentError = OrganizationValues.ValidateDepartment(request.Department);
        if (departmentError is not null)
            return Result<EmployeeSummaryResponse>.Failure(departmentError);

        var designationError = OrganizationValues.ValidateDesignation(request.Designation);
        if (designationError is not null)
            return Result<EmployeeSummaryResponse>.Failure(designationError);

        user.FullName = request.FullName;
        user.Email = request.Email;
        user.Department = OrganizationValues.NormalizeDepartment(request.Department);
        user.Designation = OrganizationValues.NormalizeDesignation(request.Designation);

        unitOfWork.Users.Update(user);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<EmployeeSummaryResponse>.Success(new EmployeeSummaryResponse(
            employee.Id, user.FullName, user.Email,
            user.Department, user.Designation, user.Role.ToString(),
            employee.Status.ToString(), user.IsActive
        ));
    }
}

using PRM.Application.Common;
using PRM.Application.DTOs.Manager;
using PRM.Application.Interfaces.Repository;
using PRM.Application.Interfaces.Service;
using PRM.Domain.Entities;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Manager;

internal static class AllocationRequestValidator
{
    internal sealed record PreparedAllocation(
        Employee Employee,
        User EmployeeUser,
        Project Project,
        int ExistingUtilisation);

    internal static async Task<Result<PreparedAllocation>> PrepareAsync(
        CreateAllocationRequest request,
        int requestingManagerEmployeeId,
        IUnitOfWork unitOfWork,
        IQueryService queryService,
        CancellationToken ct)
    {
        var utilError = ValidationRules.ValidateUtilisation(request.UtilisationPercentage);
        if (utilError is not null)
            return Result<PreparedAllocation>.Failure(utilError);

        var dateError = ValidationRules.ValidateAllocationDateRange(request.FromDate, request.ToDate);
        if (dateError is not null)
            return Result<PreparedAllocation>.Failure(dateError);

        var employee = await unitOfWork.Employees.GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<PreparedAllocation>.Failure($"Employee with ID {request.EmployeeId} not found.");

        var employeeUser = await unitOfWork.Users.GetByIdAsync(request.EmployeeId, ct);
        if (employeeUser is null || !employeeUser.IsActive)
            return Result<PreparedAllocation>.Failure($"Active employee with ID {request.EmployeeId} not found.");

        if (employeeUser.Role != UserRole.Employee)
            return Result<PreparedAllocation>.Failure("Only users with the Employee role can be allocated to projects.");

        if (employee.ManagerId != requestingManagerEmployeeId)
            return Result<PreparedAllocation>.Failure("You can only allocate employees who report to you.");

        var project = await unitOfWork.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null)
            return Result<PreparedAllocation>.Failure($"Project with ID {request.ProjectId} not found.");

        if (project.ManagerId != requestingManagerEmployeeId)
            return Result<PreparedAllocation>.Failure("You can only allocate resources to your own projects.");

        if (project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.OnHold)
            return Result<PreparedAllocation>.Failure($"Cannot allocate to a project with status '{project.Status}'.");

        var projectRangeError = ValidationRules.ValidateAllocationWithinProject(
            request.FromDate, request.ToDate, project.StartDate, project.EndDate);
        if (projectRangeError is not null)
            return Result<PreparedAllocation>.Failure(projectRangeError);

        var duplicateOnProject = await queryService.HasOverlappingAllocationOnProjectAsync(
            request.EmployeeId, request.ProjectId, request.FromDate, request.ToDate, null, ct);
        if (duplicateOnProject)
            return Result<PreparedAllocation>.Failure(
                $"{employeeUser.FullName} is already allocated to {project.Name} for overlapping dates.");

        var existingUtil = await queryService.GetEmployeeUtilisationInPeriodAsync(
            request.EmployeeId, request.FromDate, request.ToDate, null, ct);

        return Result<PreparedAllocation>.Success(new PreparedAllocation(
            employee, employeeUser, project, existingUtil));
    }

    internal static bool WouldOverAllocate(int existingUtilisation, int requestedUtilisation) =>
        existingUtilisation + requestedUtilisation > 100;
}

using PRM.Application.DTOs.Manager;
using PRM.Application.Interfaces.Repository;
using PRM.Application.Interfaces.Service;
using PRM.Application.Common;
using PRM.Domain.Entities;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Manager;

public class CreateAllocationUseCase(
    IUnitOfWork unitOfWork,
    IQueryService queryService,
    IProjectHealthService projectHealthService)
{
    public async Task<Result<CreateAllocationResponse>> ExecuteAsync(
        CreateAllocationRequest request,
        int requestingManagerEmployeeId,
        CancellationToken ct = default)
    {
        var prepared = await AllocationRequestValidator.PrepareAsync(
            request, requestingManagerEmployeeId, unitOfWork, queryService, ct);

        if (!prepared.IsSuccess)
            return Result<CreateAllocationResponse>.Failure(prepared.Error!);

        var data = prepared.Data!;
        if (AllocationRequestValidator.WouldOverAllocate(data.ExistingUtilisation, request.UtilisationPercentage))
            return Result<CreateAllocationResponse>.Failure(
                $"{data.EmployeeUser.FullName} is already at {data.ExistingUtilisation}% in this period. " +
                $"Adding {request.UtilisationPercentage}% would exceed 100%.");

        var allocation = new Allocation
        {
            EmployeeId = request.EmployeeId,
            ProjectId = request.ProjectId,
            UtilisationPercentage = request.UtilisationPercentage,
            FromDate = request.FromDate,
            ToDate = request.ToDate
        };

        await unitOfWork.Allocations.AddAsync(allocation, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (AllocationDateHelper.IsActiveOnDate(allocation, today)
            && data.Employee.Status == EmployeeStatus.Bench)
        {
            data.Employee.Status = EmployeeStatus.Allocated;
            unitOfWork.Employees.Update(data.Employee);
        }

        await unitOfWork.SaveChangesAsync(ct);

        await projectHealthService.RecalculateHealthAsync(request.ProjectId, ct);

        var newTotalUtil = data.ExistingUtilisation + request.UtilisationPercentage;

        return Result<CreateAllocationResponse>.Success(new CreateAllocationResponse(
            AllocationId: allocation.Id,
            EmployeeName: data.EmployeeUser.FullName,
            ProjectName: data.Project.Name,
            UtilisationPercentage: allocation.UtilisationPercentage,
            FromDate: allocation.FromDate,
            ToDate: allocation.ToDate,
            NewTotalUtilisation: newTotalUtil
        ));
    }
}

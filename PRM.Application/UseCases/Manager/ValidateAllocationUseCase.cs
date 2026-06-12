using PRM.Application.DTOs.Manager;
using PRM.Application.Interfaces.Repository;
using PRM.Application.Interfaces.Service;

namespace PRM.Application.UseCases.Manager;

public class ValidateAllocationUseCase(IUnitOfWork unitOfWork, IQueryService queryService)
{
    public async Task<Result<AllocationValidationResponse>> ExecuteAsync(
        CreateAllocationRequest request,
        int requestingManagerEmployeeId,
        CancellationToken ct = default)
    {
        var prepared = await AllocationRequestValidator.PrepareAsync(
            request, requestingManagerEmployeeId, unitOfWork, queryService, ct);

        if (!prepared.IsSuccess)
            return Result<AllocationValidationResponse>.Failure(prepared.Error!);

        var data = prepared.Data!;
        var total = data.ExistingUtilisation + request.UtilisationPercentage;
        var isValid = !AllocationRequestValidator.WouldOverAllocate(
            data.ExistingUtilisation, request.UtilisationPercentage);

        var message = isValid
            ? $"{data.EmployeeUser.FullName} total in this period: {data.ExistingUtilisation}% + {request.UtilisationPercentage}% = {total}%   Valid"
            : $"{data.EmployeeUser.FullName} would be over-allocated: {data.ExistingUtilisation}% + {request.UtilisationPercentage}% = {total}% (max 100%)";

        return Result<AllocationValidationResponse>.Success(new AllocationValidationResponse(
            EmployeeId: request.EmployeeId,
            EmployeeName: data.EmployeeUser.FullName,
            ExistingUtilisationInPeriod: data.ExistingUtilisation,
            RequestedUtilisation: request.UtilisationPercentage,
            TotalIfConfirmed: total,
            IsValid: isValid,
            ValidationMessage: message
        ));
    }
}

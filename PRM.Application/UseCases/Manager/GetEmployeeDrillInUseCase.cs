using PRM.Application.DTOs.Manager;
using PRM.Application.Interfaces.Service;

namespace PRM.Application.UseCases.Manager
{
    public class GetEmployeeDrillInUseCase(IQueryService queryService)
    {
        private const int RecentWeeksCount = 4;

        public async Task<Result<EmployeeDrillInResponse>> ExecuteAsync(
            int employeeId, int managerEmployeeId, CancellationToken ct = default)
        {
            if (!await queryService.IsEmployeeUnderManagerAsync(employeeId, managerEmployeeId, ct))
                return Result<EmployeeDrillInResponse>.Failure("Employee is not on your team.");

            var detail = await queryService.GetEmployeeWithSkillsAsync(employeeId, ct);
            if (detail is null)
                return Result<EmployeeDrillInResponse>.Failure($"Employee with ID {employeeId} not found.");

            var allocations = (await queryService.GetActiveAllocationsForEmployeeAsync(employeeId, ct)).ToList();
            var totalUtil = allocations.Sum(a => a.UtilisationPercent);
            var recentTags = await queryService.GetRecentActivityTagsAsync(employeeId, RecentWeeksCount, ct);
            var computedStatus = totalUtil > 0 ? "Allocated" : "Bench";

            return Result<EmployeeDrillInResponse>.Success(new EmployeeDrillInResponse(
                Id: detail.Id,
                FullName: detail.FullName,
                Department: detail.Department,
                Designation: detail.Designation,
                Status: computedStatus,
                TotalUtilisationPercent: totalUtil,
                ProfileSkills: detail.Skills.Select(s => s.SkillName),
                ActiveAllocations: allocations,
                RecentActivityTags: recentTags
            ));
        }
    }
}

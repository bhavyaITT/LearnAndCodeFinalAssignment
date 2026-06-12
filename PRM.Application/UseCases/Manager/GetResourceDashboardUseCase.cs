using PRM.Application.DTOs.Manager;
using PRM.Application.Interfaces.Service;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Manager
{
    public class GetResourceDashboardUseCase(IQueryService queryService)
    {
        public async Task<Result<ResourceDashboardResponse>> ExecuteAsync(
            int managerEmployeeId, CancellationToken ct = default)
        {
            var team = (await queryService.GetTeamEmployeesForDashboardAsync(managerEmployeeId, ct)).ToList();

            var bench = team
                .Where(e => e.Status == EmployeeStatus.Bench.ToString())
                .OrderBy(e => e.FullName)
                .ToList();

            var active = team
                .Where(e => e.Status == EmployeeStatus.Allocated.ToString())
                .OrderByDescending(e => e.TotalUtilisationPercent)
                .ToList();

            var benchResponses = new List<BenchEmployeeResponse>();
            foreach (var e in bench)
            {
                var detail = await queryService.GetEmployeeWithSkillsAsync(e.EmployeeId, ct);
                var skills = detail?.Skills.Select(s => s.SkillName).Take(5) ?? [];
                benchResponses.Add(new BenchEmployeeResponse(
                    e.EmployeeId, e.FullName, e.Department, e.Designation, skills
                ));
            }

            var activeResponses = active.Select(e =>
            {
                var free = 100 - e.TotalUtilisationPercent;
                var label = e.TotalUtilisationPercent >= 100
                    ? "FULL"
                    : $"{free}% free";

                return new ActiveEmployeeResponse(
                    e.EmployeeId, e.FullName, e.Department,
                    e.TotalUtilisationPercent, free, label
                );
            }).ToList();

            return Result<ResourceDashboardResponse>.Success(new ResourceDashboardResponse(
                BenchEmployees: benchResponses,
                ActiveEmployees: activeResponses,
                BenchCount: bench.Count,
                OverUtilisedCount: active.Count(e => e.TotalUtilisationPercent > 100),
                PartialCount: active.Count(e => e.TotalUtilisationPercent < 100)
            ));
        }
    }
}

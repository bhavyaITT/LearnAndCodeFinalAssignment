using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRM.Application.DTOs;
using PRM.Application.UseCases.AI;
using PRM.Application.UseCases.Manager;
using PRM.Infrastructure.AI.Prompts;

namespace PRM.API.Controllers
{
    [ApiController]
    [Route("api/ai")]
    [Authorize(Roles = "Manager")]
    public class AiAssistantController(
        SkillMatchUseCase skillMatch,
        TeamSkillMatchUseCase teamSkillMatch,
        RiskSummaryUseCase riskSummary,
        ResolveEmployeeIdUseCase resolveEmployeeId) : ControllerBase
    {
        /// <summary>
        /// POST /api/ai/skill-match
        /// Finds best-fit employees on the manager's team for a plain-English requirement.
        /// Filters candidates by free capacity before calling AI.
        /// </summary>
        [HttpPost("skill-match")]
        public async Task<IActionResult> SkillMatch(
            [FromBody] SkillMatchRequest request,
            CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var managerResult = await resolveEmployeeId.ExecuteAsync(userId.Value, ct);
            if (!managerResult.IsSuccess)
                return BadRequest(new { message = managerResult.Error });

            var result = await skillMatch.ExecuteAsync(request, managerResult.Data, ct);

            if (!result.IsSuccess)
                return BadRequest(new { message = result.Error });

            return Ok(result.Data);
        }

        /// <summary>
        /// POST /api/ai/team-staffing
        /// Infers roles from a plain-English team requirement and staffs from organisation-wide bench.
        /// </summary>
        [HttpPost("team-staffing")]
        public async Task<IActionResult> TeamStaffing(
            [FromBody] TeamStaffingRequest request,
            CancellationToken ct)
        {
            var result = await teamSkillMatch.ExecuteAsync(request, ct);

            if (!result.IsSuccess)
                return BadRequest(new { message = result.Error });

            return Ok(result.Data);
        }

        /// <summary>
        /// GET /api/ai/risk-summary/{projectId}
        /// Generates a plain-English health analysis for a project.
        /// </summary>
        [HttpGet("risk-summary/{projectId:int}")]
        public async Task<IActionResult> RiskSummary(int projectId, CancellationToken ct)
        {
            var result = await riskSummary.ExecuteAsync(projectId, ct);

            if (!result.IsSuccess)
                return BadRequest(new { message = result.Error });

            return Ok(result.Data);
        }
    }
}

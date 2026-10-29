using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRM.Application.UseCases.Manager;

namespace PRM.API.Controllers
{
    [ApiController]
    [Route("api/manager/dashboard")]
    [Authorize(Roles = "Manager")]
    public class ResourceDashboardController(
    GetResourceDashboardUseCase getDashboard,
    GetEmployeeDrillInUseCase getDrillIn,
    ResolveEmployeeIdUseCase resolveEmployeeId) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var managerResult = await resolveEmployeeId.ExecuteAsync(userId.Value, ct);
            if (!managerResult.IsSuccess)
                return BadRequest(new { message = managerResult.Error });

            var result = await getDashboard.ExecuteAsync(managerResult.Data, ct);
            return Ok(result.Data);
        }

        [HttpGet("employees/{id:int}")]
        public async Task<IActionResult> DrillIn(int id, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var managerResult = await resolveEmployeeId.ExecuteAsync(userId.Value, ct);
            if (!managerResult.IsSuccess)
                return BadRequest(new { message = managerResult.Error });

            var result = await getDrillIn.ExecuteAsync(id, managerResult.Data, ct);

            if (!result.IsSuccess)
                return NotFound(new { message = result.Error });

            return Ok(result.Data);
        }

        /// <summary>Team employees with current/future allocations — direct allocation screen.</summary>
        [HttpGet("team-allocations")]
        public async Task<IActionResult> TeamAllocations(CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var managerResult = await resolveEmployeeId.ExecuteAsync(userId.Value, ct);
            if (!managerResult.IsSuccess)
                return BadRequest(new { message = managerResult.Error });

            var rows = await getDashboard.ExecuteTeamAllocationsAsync(managerResult.Data, ct);
            return Ok(rows);
        }
    }
}

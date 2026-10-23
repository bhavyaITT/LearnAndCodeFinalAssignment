using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRM.Application.UseCases.Manager;

namespace PRM.API.Controllers
{
    [ApiController]
    [Route("api/manager/timesheets")]
    [Authorize(Roles = "Manager")]
    public class ManagerTimesheetsController(
        GetTeamTimesheetsUseCase getTeamTimesheets,
        RestoreTimesheetSubmissionUseCase restoreSubmission,
        ResolveEmployeeIdUseCase resolveEmployeeId) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetTeamTimesheets(
            [FromQuery] DateOnly? weekStart,
            CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var employeeIdResult = await resolveEmployeeId.ExecuteAsync(userId.Value, ct);
            if (!employeeIdResult.IsSuccess)
                return BadRequest(new { message = employeeIdResult.Error });

            var week = weekStart ?? GetCurrentMonday();
            var result = await getTeamTimesheets.ExecuteAsync(employeeIdResult.Data, week, ct);
            return Ok(result.Data);
        }

        [HttpGet("employees/{employeeId:int}")]
        public async Task<IActionResult> GetEmployeeDetail(
            int employeeId,
            [FromQuery] DateOnly weekStart,
            CancellationToken ct)
        {
            var result = await getTeamTimesheets.GetDetailAsync(employeeId, weekStart, ct);

            if (!result.IsSuccess)
                return NotFound(new { message = result.Error });

            return Ok(result.Data);
        }

        [HttpPut("employees/{employeeId:int}/restore-submission")]
        public async Task<IActionResult> RestoreSubmission(int employeeId, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (userId is null) return Unauthorized();

            var managerResult = await resolveEmployeeId.ExecuteAsync(userId.Value, ct);
            if (!managerResult.IsSuccess)
                return BadRequest(new { message = managerResult.Error });

            var result = await restoreSubmission.ExecuteAsync(employeeId, managerResult.Data, ct);
            if (!result.IsSuccess)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Timesheet submission access restored." });
        }

        private static DateOnly GetCurrentMonday()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var dayOfWeek = (int)today.DayOfWeek;
            var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
            return today.AddDays(-daysFromMonday);
        }
    }
}

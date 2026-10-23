using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PRM.Application.UseCases;

namespace PRM.API.Controllers
{
    [ApiController]
    [Route("api/allocations")]
    [Authorize(Roles = "Admin,Manager")]  
    public class AllocationsController(GetAllAllocationsUseCase getAllAllocations) : ControllerBase
    {
        /// <summary>
        /// GET /api/allocations?employeeId=1&projectId=2&activeOnly=true
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? employeeId,
            [FromQuery] int? projectId,
            [FromQuery] bool activeOnly = true,
            CancellationToken ct = default)
        {
            var result = await getAllAllocations.ExecuteAsync(employeeId, projectId, activeOnly, ct);
            return Ok(result.Data);
        }
    }
}

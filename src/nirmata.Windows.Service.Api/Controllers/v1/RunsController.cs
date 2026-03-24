using Microsoft.AspNetCore.Mvc;
using nirmata.Windows.Service.Api;

namespace nirmata.Windows.Service.Api.Controllers.V1;

[ApiController]
[Route("api/v1/runs")]
public class RunsController(DaemonRuntimeState state) : ControllerBase
{
    /// <summary>
    /// Returns engine-level run summaries, optionally filtered by taskId or status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RunSummary>), StatusCodes.Status200OK)]
    public IActionResult GetAll(
        [FromQuery] string? taskId,
        [FromQuery] string? status)
    {
        IEnumerable<RunSummary> result = state.Runs;

        if (!string.IsNullOrEmpty(taskId))
            result = result.Where(r => r.TaskId == taskId);

        if (!string.IsNullOrEmpty(status))
            result = result.Where(r => r.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        return Ok(result.ToList());
    }
}

using Microsoft.AspNetCore.Mvc;
using nirmata.Windows.Service.Api;

namespace nirmata.Windows.Service.Api.Controllers.V1;

[ApiController]
[Route("api/v1/logs")]
public class LogsController(DaemonRuntimeState state) : ControllerBase
{
    /// <summary>
    /// Returns recent host log entries captured since daemon startup.
    /// Poll this endpoint for a live view of daemon activity.
    /// </summary>
    /// <param name="tail">Limit the response to the N most recent entries (default: all).</param>
    /// <param name="level">Optional minimum log level filter (e.g. "warning", "error").</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HostLogEntry>), StatusCodes.Status200OK)]
    public IActionResult GetLogs(
        [FromQuery] int? tail,
        [FromQuery] string? level)
    {
        IEnumerable<HostLogEntry> entries = state.GetLogEntries(tail);

        if (!string.IsNullOrEmpty(level))
        {
            entries = entries.Where(e =>
                e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(entries.ToList());
    }
}

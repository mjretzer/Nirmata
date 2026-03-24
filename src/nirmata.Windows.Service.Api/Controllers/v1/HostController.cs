using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class HostLogLine
{
    public int Id { get; init; }
    public string Ts { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty; // "info" | "warn" | "error"
    public string Msg { get; init; } = string.Empty;
}

public sealed class ApiSurface
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool Ok { get; init; }
    public int? LatencyMs { get; init; }
    public string? Reason { get; init; }
}

[ApiController]
[Route("api/v1/host")]
public class HostController : ControllerBase
{
    private static readonly List<HostLogLine> _logs = [];
    private static readonly List<ApiSurface> _surfaces = [];

    /// <summary>
    /// Returns host/daemon log lines, newest first.
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(IReadOnlyList<HostLogLine>), StatusCodes.Status200OK)]
    public IActionResult GetLogs([FromQuery] int limit = 100)
    {
        var result = _logs.TakeLast(limit).Reverse().ToList();
        return Ok(result);
    }

    /// <summary>
    /// Returns the status of all registered API surfaces.
    /// </summary>
    [HttpGet("surfaces")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiSurface>), StatusCodes.Status200OK)]
    public IActionResult GetSurfaces() => Ok(_surfaces);
}

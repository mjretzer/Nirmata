using Microsoft.AspNetCore.Mvc;
using nirmata.Windows.Service.Api;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class OkResponse
{
    public bool Ok { get; init; }
}

public sealed class DaemonApiSurface
{
    /// <summary>Logical name for this API surface group (e.g. "health", "commands").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Base path for this surface (e.g. "/api/v1/health").</summary>
    public string BasePath { get; init; } = string.Empty;

    /// <summary>HTTP methods exposed by this surface.</summary>
    public string[] Methods { get; init; } = [];
}

public sealed class ServiceStatusResponse
{
    public bool Ok { get; init; }

    /// <summary>
    /// Current service lifecycle state.
    /// Known values: "Running", "Stopped", "Starting", "Stopping".
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// API surfaces exposed by this daemon instance.
    /// </summary>
    public DaemonApiSurface[] Surfaces { get; init; } = [];
}

[ApiController]
[Route("api/v1/service")]
public class ServiceController(DaemonRuntimeState state) : ControllerBase
{
    private static readonly DaemonApiSurface[] _surfaces =
    [
        new() { Name = "health",      BasePath = "/api/v1/health",      Methods = ["GET"] },
        new() { Name = "service",     BasePath = "/api/v1/service",      Methods = ["GET", "PUT"] },
        new() { Name = "commands",    BasePath = "/api/v1/commands",     Methods = ["POST"] },
        new() { Name = "runs",        BasePath = "/api/v1/runs",         Methods = ["GET"] },
        new() { Name = "logs",        BasePath = "/api/v1/logs",         Methods = ["GET"] },
        new() { Name = "diagnostics", BasePath = "/api/v1/diagnostics",  Methods = ["GET", "DELETE"] },
    ];

    /// <summary>
    /// Returns the current service lifecycle status and available API surfaces.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ServiceStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        return Ok(new ServiceStatusResponse { Ok = true, Status = "Running", Surfaces = _surfaces });
    }

    /// <summary>
    /// Registers or updates the host profile for this Agent Manager instance.
    /// </summary>
    [HttpPut("host-profile")]
    [ProducesResponseType(typeof(OkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult SetHostProfile([FromBody] HostProfileRequest request)
    {
        state.HostProfile = request;
        return Ok(new OkResponse { Ok = true });
    }

    /// <summary>
    /// Starts the agent service. Stubbed — returns stable shape.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(OkResponse), StatusCodes.Status200OK)]
    public IActionResult Start()
    {
        return Ok(new OkResponse { Ok = true });
    }

    /// <summary>
    /// Stops the agent service. Stubbed — returns stable shape.
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(typeof(OkResponse), StatusCodes.Status200OK)]
    public IActionResult Stop()
    {
        return Ok(new OkResponse { Ok = true });
    }

    /// <summary>
    /// Restarts the agent service. Stubbed — returns stable shape.
    /// </summary>
    [HttpPost("restart")]
    [ProducesResponseType(typeof(OkResponse), StatusCodes.Status200OK)]
    public IActionResult Restart()
    {
        return Ok(new OkResponse { Ok = true });
    }
}

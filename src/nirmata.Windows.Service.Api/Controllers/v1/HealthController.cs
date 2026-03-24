using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class HealthResponse
{
    public bool Ok { get; init; }
    public string Version { get; init; } = string.Empty;
    public double UptimeMs { get; init; }
}

[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private static readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns the health status of the Agent Manager service.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        var version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var uptimeMs = Math.Max(0.0, (DateTimeOffset.UtcNow - _startTime).TotalMilliseconds);

        return Ok(new HealthResponse
        {
            Ok = true,
            Version = version,
            UptimeMs = uptimeMs
        });
    }
}

using nirmata.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace nirmata.Api.Controllers;

/// <summary>
/// Response model for detailed health check endpoint.
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// Overall health status of the API.
    /// </summary>
    public string Status { get; set; } = "Unknown";

    /// <summary>
    /// UTC timestamp when the health check was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Total time taken to perform all health checks in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Health status of individual dependencies.
    /// </summary>
    public Dictionary<string, DependencyHealth> Dependencies { get; set; } = new();
}

/// <summary>
/// Health status of a single dependency.
/// </summary>
public class DependencyHealth
{
    /// <summary>
    /// Health status of the dependency (Healthy, Degraded, Unhealthy).
    /// </summary>
    public string Status { get; set; } = "Unknown";

    /// <summary>
    /// Time taken to check this dependency in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Optional error message if the dependency check failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Response model for the simple health check endpoint.
/// </summary>
public class HealthStatusResponse
{
    /// <summary>
    /// Overall health status of the API.
    /// </summary>
    public string Status { get; set; } = "Unknown";

    /// <summary>
    /// UTC timestamp when the health check was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Controller providing health check endpoints for monitoring and observability.
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : nirmataController
{
    private readonly nirmataDbContext _dbContext;

    public HealthController(nirmataDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Performs detailed health checks on all system dependencies including database connectivity.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new HealthCheckResponse
        {
            Timestamp = DateTime.UtcNow
        };

        // Check database connectivity
        var dbStopwatch = Stopwatch.StartNew();
        var dbHealth = await CheckDatabaseHealthAsync(cancellationToken);
        dbStopwatch.Stop();

        response.Dependencies["database"] = new DependencyHealth
        {
            Status = dbHealth.IsHealthy ? "Healthy" : "Unhealthy",
            DurationMs = dbStopwatch.ElapsedMilliseconds,
            Error = dbHealth.Error
        };

        // Determine overall status
        response.Status = dbHealth.IsHealthy ? "Healthy" : "Unhealthy";

        stopwatch.Stop();
        response.TotalDurationMs = stopwatch.ElapsedMilliseconds;

        var statusCode = dbHealth.IsHealthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }

    /// <summary>
    /// Returns a simple health status indicating whether the API is operational.
    /// </summary>
    [HttpGet("simple")]
    [ProducesResponseType(typeof(HealthStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetSimpleHealth()
    {
        return Ok(new HealthStatusResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Alias route for detailed health checks.
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> GetDetailedHealthAsync(CancellationToken cancellationToken)
        => GetHealthAsync(cancellationToken);

    private async Task<(bool IsHealthy, string? Error)> CheckDatabaseHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

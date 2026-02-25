using Gmsd.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Gmsd.Api.Controllers;

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
/// Controller providing detailed health check endpoints for monitoring and observability.
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : GmsdController
{
    private readonly GmsdDbContext _dbContext;

    public HealthController(GmsdDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Performs detailed health checks on all system dependencies.
    /// </summary>
    /// <returns>Detailed health status including database connectivity and timing metrics.</returns>
    [HttpGet]
    public async Task<IActionResult> GetDetailedHealth(CancellationToken cancellationToken)
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

    private async Task<(bool IsHealthy, string? Error)> CheckDatabaseHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Attempt a simple database query to verify connectivity
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

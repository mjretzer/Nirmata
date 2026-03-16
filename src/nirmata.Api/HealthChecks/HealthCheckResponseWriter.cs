using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace nirmata.Api.HealthChecks;

/// <summary>
/// Custom health check response writer that outputs detailed JSON including dependency status and timing.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task WriteDetailedResponse(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            Status = report.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            TotalDurationMs = (long)report.TotalDuration.TotalMilliseconds,
            Dependencies = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    Status = e.Value.Status.ToString(),
                    DurationMs = (long)e.Value.Duration.TotalMilliseconds,
                    Description = e.Value.Description,
                    Error = e.Value.Exception?.Message
                })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, Options));
    }
}

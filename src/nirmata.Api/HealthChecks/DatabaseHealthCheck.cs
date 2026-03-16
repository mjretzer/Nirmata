using nirmata.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace nirmata.Api.HealthChecks;

/// <summary>
/// Custom health check that verifies database connectivity using EF Core.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly nirmataDbContext _dbContext;

    public DatabaseHealthCheck(nirmataDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("Database connectivity verified");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connectivity failed", ex);
        }
    }
}

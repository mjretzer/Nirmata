using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gmsd.Agents.Workers;

/// <summary>
/// Background hosted service that drives agent execution queues/schedulers/pollers.
/// </summary>
public sealed class AgentRuntimeWorker : BackgroundService
{
    private readonly ILogger<AgentRuntimeWorker> _logger;

    public AgentRuntimeWorker(ILogger<AgentRuntimeWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent runtime worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Agent runtime loop placeholder
                // This worker drives the execution of agent tasks from queues
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agent runtime worker stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in agent runtime worker");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Gmsd.Aos.Public;

namespace Gmsd.Aos.Configuration;

/// <summary>
/// Loads concurrency configuration from .aos/config/concurrency.json
/// </summary>
public sealed class ConcurrencyConfigurationLoader
{
    private readonly IWorkspace _workspace;
    private readonly ILogger<ConcurrencyConfigurationLoader> _logger;

    public ConcurrencyConfigurationLoader(IWorkspace workspace, ILogger<ConcurrencyConfigurationLoader> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads concurrency configuration from file or returns defaults.
    /// </summary>
    public ConcurrencyOptions Load()
    {
        var configPath = Path.Combine(_workspace.AosRootPath, "config", "concurrency.json");

        if (!File.Exists(configPath))
        {
            _logger.LogInformation("Concurrency configuration not found at {ConfigPath}, using defaults", configPath);
            return new ConcurrencyOptions();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var options = JsonSerializer.Deserialize<ConcurrencyOptions>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (options == null)
            {
                _logger.LogWarning("Failed to deserialize concurrency configuration, using defaults");
                return new ConcurrencyOptions();
            }

            var validationError = options.Validate();
            if (validationError != null)
            {
                throw new InvalidOperationException($"Invalid concurrency configuration: {validationError}");
            }

            _logger.LogInformation(
                "Concurrency configuration loaded: maxParallelTasks={MaxParallelTasks}, maxParallelLlmCalls={MaxParallelLlmCalls}, taskQueueSize={TaskQueueSize}",
                options.MaxParallelTasks,
                options.MaxParallelLlmCalls,
                options.TaskQueueSize);

            return options;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading concurrency configuration from {ConfigPath}", configPath);
            throw;
        }
    }
}

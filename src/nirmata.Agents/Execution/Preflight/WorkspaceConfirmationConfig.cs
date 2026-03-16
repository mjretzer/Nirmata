using System.Text.Json;
using System.Text.Json.Serialization;

namespace nirmata.Agents.Execution.Preflight;

/// <summary>
/// Configuration loaded from workspace .aos/config file for confirmation gate thresholds.
/// </summary>
public sealed class WorkspaceConfirmationConfig
{
    /// <summary>
    /// Threshold for destructive operations (high bar due to irreversible nature).
    /// </summary>
    [JsonPropertyName("destructiveThreshold")]
    public double? DestructiveThreshold { get; init; }

    /// <summary>
    /// Threshold for write operations (file modifications, etc.).
    /// </summary>
    [JsonPropertyName("writeThreshold")]
    public double? WriteThreshold { get; init; }

    /// <summary>
    /// Threshold for ambiguous or unclear operations.
    /// </summary>
    [JsonPropertyName("ambiguousThreshold")]
    public double? AmbiguousThreshold { get; init; }

    /// <summary>
    /// General confirmation threshold fallback.
    /// </summary>
    [JsonPropertyName("confirmationThreshold")]
    public double? ConfirmationThreshold { get; init; }

    /// <summary>
    /// Whether to always confirm writes regardless of confidence.
    /// </summary>
    [JsonPropertyName("alwaysConfirmWrites")]
    public bool? AlwaysConfirmWrites { get; init; }

    /// <summary>
    /// Timeout for confirmation requests in seconds.
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Per-operation type overrides for thresholds.
    /// </summary>
    [JsonPropertyName("operationThresholds")]
    public Dictionary<string, double>? OperationThresholds { get; init; }

    /// <summary>
    /// Loads workspace configuration from .aos/config file.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root directory.</param>
    /// <returns>Loaded configuration or default if not found.</returns>
    public static WorkspaceConfirmationConfig Load(string workspaceRoot)
    {
        var configPath = Path.Combine(workspaceRoot, ".aos", "config");
        if (!File.Exists(configPath))
        {
            return new WorkspaceConfirmationConfig();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<WorkspaceConfirmationConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return config ?? new WorkspaceConfirmationConfig();
        }
        catch
        {
            // Return default config if file can't be read or parsed
            return new WorkspaceConfirmationConfig();
        }
    }

    /// <summary>
    /// Applies workspace configuration to confirmation gate options.
    /// </summary>
    /// <param name="baseOptions">The base options to merge with.</param>
    /// <returns>Merged options with workspace overrides applied.</returns>
    public ConfirmationGateOptions ApplyTo(ConfirmationGateOptions baseOptions)
    {
        return new ConfirmationGateOptions
        {
            ConfirmationThreshold = ConfirmationThreshold ?? baseOptions.ConfirmationThreshold,
            DestructiveThreshold = DestructiveThreshold ?? baseOptions.DestructiveThreshold,
            WriteThreshold = WriteThreshold ?? baseOptions.WriteThreshold,
            AmbiguousThreshold = AmbiguousThreshold ?? baseOptions.AmbiguousThreshold,
            AlwaysConfirmWrites = AlwaysConfirmWrites ?? baseOptions.AlwaysConfirmWrites,
            Timeout = TimeoutSeconds.HasValue
                ? TimeSpan.FromSeconds(TimeoutSeconds.Value)
                : baseOptions.Timeout,
            NoConfirmationCommands = baseOptions.NoConfirmationCommands
        };
    }

    /// <summary>
    /// Gets threshold for a specific operation type if configured.
    /// </summary>
    /// <param name="operationType">The operation type (e.g., "git.commit", "file.write").</param>
    /// <returns>The threshold if configured, null otherwise.</returns>
    public double? GetOperationThreshold(string operationType)
    {
        if (OperationThresholds?.TryGetValue(operationType, out var threshold) == true)
        {
            return threshold;
        }
        return null;
    }
}

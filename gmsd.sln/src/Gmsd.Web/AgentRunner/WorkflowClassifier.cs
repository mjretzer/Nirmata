using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Observability;
using System.Text.Json;
using Gmsd.Aos.Engine.Workspace;

namespace Gmsd.Web.AgentRunner;

/// <summary>
/// Direct in-process runner for executing agent workflows via the Gmsd.Agents orchestrator.
/// This is a thin wrapper that normalizes inputs and delegates to IOrchestrator.ExecuteAsync().
/// </summary>
public sealed class WorkflowClassifier
{
    private readonly IOrchestrator _orchestrator;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<WorkflowClassifier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowClassifier"/> class.
    /// </summary>
    /// <param name="orchestrator">The orchestrator to execute workflows through.</param>
    /// <param name="correlationIdProvider">Provider for generating and tracking correlation IDs.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="configuration">Configuration for accessing workspace settings.</param>
    public WorkflowClassifier(
        IOrchestrator orchestrator,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<WorkflowClassifier> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a run command by normalizing input and delegating to the orchestrator.
    /// </summary>
    /// <param name="inputRaw">The raw user input (CLI args or freeform text).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the orchestration.</returns>
    public async Task<OrchestratorResult> ExecuteAsync(string inputRaw, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inputRaw))
        {
            throw new ArgumentException("Input cannot be null or whitespace.", nameof(inputRaw));
        }

        // Ensure workspace is initialized before executing
        var workspacePath = GetWorkspacePath();
        if (!string.IsNullOrEmpty(workspacePath))
        {
            InitializeWorkspaceIfNeeded(workspacePath);
        }

        var correlationId = _correlationIdProvider.Generate();
        _correlationIdProvider.SetCurrent(correlationId);

        _logger.LogInformation("Starting agent execution with correlation ID {CorrelationId}", correlationId);

        var intent = new WorkflowIntent
        {
            InputRaw = inputRaw,
            InputNormalized = NormalizeInput(inputRaw),
            CorrelationId = correlationId
        };

        try
        {
            var result = await _orchestrator.ExecuteAsync(intent, ct);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Agent execution completed successfully. Run ID: {RunId}, Final Phase: {FinalPhase}",
                    result.RunId,
                    result.FinalPhase);
            }
            else
            {
                _logger.LogWarning(
                    "Agent execution failed. Run ID: {RunId}, Final Phase: {FinalPhase}",
                    result.RunId,
                    result.FinalPhase);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Agent execution was cancelled. Correlation ID: {CorrelationId}", correlationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent execution failed with exception. Correlation ID: {CorrelationId}", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Normalizes raw input into a consistent command format for the orchestrator.
    /// </summary>
    /// <param name="inputRaw">The raw input string.</param>
    /// <returns>Normalized command string, or null if no normalization is needed.</returns>
    private static string? NormalizeInput(string inputRaw)
    {
        var trimmed = inputRaw.Trim();

        // If already looks like a structured command (starts with known prefixes), no normalization needed
        if (trimmed.StartsWith("run ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("plan ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("help", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("status", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Default normalization: treat freeform text as a "run" command
        return $"run {trimmed}";
    }

    /// <summary>
    /// Gets the currently selected workspace path from configuration.
    /// </summary>
    private string? GetWorkspacePath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configPath = Path.Combine(appData, "Gmsd", "workspace-config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("selectedWorkspacePath", out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read workspace configuration");
        }
        return null;
    }

    /// <summary>
    /// Initializes the workspace .aos structure if it doesn't exist.
    /// </summary>
    private void InitializeWorkspaceIfNeeded(string workspacePath)
    {
        try
        {
            var result = AosWorkspaceBootstrapper.EnsureInitialized(workspacePath);
            _logger.LogInformation("{Outcome}: {AosRootPath}", result.Outcome, result.AosRootPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize workspace");
            throw;
        }
    }
}

using Microsoft.Extensions.Logging;

namespace Gmsd.Agents.Observability;

/// <summary>
/// Structured logging helpers for GMSD Agents.
/// </summary>
public static class AgentsLoggerExtensions
{
    private const string CorrelationIdKey = "CorrelationId";
    private const string RunIdKey = "RunId";
    private const string WorkflowNameKey = "WorkflowName";

    /// <summary>
    /// Begins a correlation ID scope for logging.
    /// </summary>
    public static IDisposable? BeginCorrelationIdScope(this ILogger logger, string correlationId)
    {
        return logger.BeginScope(new Dictionary<string, object> { [CorrelationIdKey] = correlationId });
    }

    /// <summary>
    /// Begins a run scope for logging with correlation ID and run ID.
    /// </summary>
    public static IDisposable? BeginRunScope(this ILogger logger, string correlationId, string runId)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdKey] = correlationId,
            [RunIdKey] = runId
        });
    }

    /// <summary>
    /// Begins a full run scope for logging with all context.
    /// </summary>
    public static IDisposable? BeginRunScope(
        this ILogger logger,
        string correlationId,
        string runId,
        string workflowName)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdKey] = correlationId,
            [RunIdKey] = runId,
            [WorkflowNameKey] = workflowName
        });
    }

    /// <summary>
    /// Logs the start of a run with structured context.
    /// </summary>
    public static void LogRunStarted(this ILogger logger, string correlationId, string runId, string workflowName)
    {
        using var scope = logger.BeginRunScope(correlationId, runId, workflowName);
        logger.LogInformation("Run started: {WorkflowName} (RunId: {RunId}, CorrelationId: {CorrelationId})",
            workflowName, runId, correlationId);
    }

    /// <summary>
    /// Logs the completion of a run with structured context.
    /// </summary>
    public static void LogRunCompleted(this ILogger logger, string correlationId, string runId, string workflowName, bool success)
    {
        using var scope = logger.BeginRunScope(correlationId, runId, workflowName);
        if (success)
        {
            logger.LogInformation("Run completed successfully: {WorkflowName} (RunId: {RunId})",
                workflowName, runId);
        }
        else
        {
            logger.LogError("Run failed: {WorkflowName} (RunId: {RunId})",
                workflowName, runId);
        }
    }

    /// <summary>
    /// Logs an LLM interaction with structured context.
    /// </summary>
    public static void LogLlmInteraction(this ILogger logger, string correlationId, string runId, string operation, long? latencyMs = null)
    {
        using var scope = logger.BeginRunScope(correlationId, runId);
        if (latencyMs.HasValue)
        {
            logger.LogDebug("LLM {Operation} completed in {LatencyMs}ms (RunId: {RunId})",
                operation, latencyMs.Value, runId);
        }
        else
        {
            logger.LogDebug("LLM {Operation} started (RunId: {RunId})",
                operation, runId);
        }
    }
}

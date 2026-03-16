namespace nirmata.Aos.Contracts.Tools;

/// <summary>
/// Execution context for tool invocations.
/// Provides correlation IDs and run information for tracing and auditability.
/// </summary>
public sealed class ToolContext
{
    /// <summary>
    /// The unique identifier for the current run/execution.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Correlation ID for tracing the request across multiple tools/services.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Parent operation or tool invocation ID, if this invocation is part of a chain.
    /// </summary>
    public string? ParentOperationId { get; init; }

    /// <summary>
    /// Timestamp when the context was created (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional workspace or session identifier.
    /// </summary>
    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Additional context properties for extensibility.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}

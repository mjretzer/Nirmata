namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Configuration options for streaming orchestration behavior.
/// Controls event emission, filtering, and buffering characteristics.
/// </summary>
public sealed class StreamingOrchestrationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to emit intent classification events.
    /// Default is true.
    /// </summary>
    public bool EmitIntentClassified { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to emit gate selection events.
    /// Default is true.
    /// </summary>
    public bool EmitGateSelected { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to emit tool call events.
    /// Default is true.
    /// </summary>
    public bool EmitToolCalls { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to emit phase lifecycle events.
    /// Default is true.
    /// </summary>
    public bool EmitPhaseLifecycle { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to emit run lifecycle events.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// Note: Run events are automatically suppressed for chat-only interactions
    /// regardless of this setting.
    /// </remarks>
    public bool EmitRunLifecycle { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to emit assistant message delta events.
    /// Default is true.
    /// </summary>
    public bool EmitAssistantDeltas { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of events to buffer before applying backpressure.
    /// Default is 1000. Set to 0 for unbounded buffering (not recommended for production).
    /// </summary>
    public int EventBufferCapacity { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether to include raw tool parameters in tool.call events.
    /// Default is false for security (parameters may contain sensitive data).
    /// </summary>
    public bool IncludeToolParameters { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to include full reasoning text in events.
    /// When false, only summarized reasoning is included.
    /// Default is true.
    /// </summary>
    public bool IncludeFullReasoning { get; set; } = true;

    /// <summary>
    /// Gets or sets the correlation ID for this streaming session.
    /// If not provided, a new GUID will be generated.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether events should include sequence numbers.
    /// Default is true. Disable for scenarios where ordering is handled externally.
    /// </summary>
    public bool EnableSequenceNumbers { get; set; } = true;

    /// <summary>
    /// Gets default options with all event types enabled.
    /// </summary>
    public static StreamingOrchestrationOptions Default => new();

    /// <summary>
    /// Gets options optimized for chat-only interactions (no run lifecycle events).
    /// </summary>
    public static StreamingOrchestrationOptions ChatOnly => new()
    {
        EmitRunLifecycle = false,
        EmitPhaseLifecycle = false,
        EmitToolCalls = false
    };

    /// <summary>
    /// Gets minimal options that only emit assistant messages.
    /// </summary>
    public static StreamingOrchestrationOptions Minimal => new()
    {
        EmitIntentClassified = false,
        EmitGateSelected = false,
        EmitToolCalls = false,
        EmitPhaseLifecycle = false,
        EmitRunLifecycle = false,
        IncludeFullReasoning = false,
        EnableSequenceNumbers = false
    };
}

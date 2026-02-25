using Gmsd.Aos.Contracts.Tools;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// AsyncLocal context for flowing IToolEventSink through the execution call chain.
/// This allows the singleton Kernel to access request-scoped event sinks during tool invocation.
/// </summary>
public static class ToolEventSinkContext
{
    private static readonly AsyncLocal<IToolEventSink?> _currentSink = new();
    private static readonly AsyncLocal<string?> _currentCorrelationId = new();
    private static readonly AsyncLocal<string?> _currentPhaseContext = new();

    /// <summary>
    /// Gets or sets the current tool event sink for this async context.
    /// </summary>
    public static IToolEventSink? Current
    {
        get => _currentSink.Value;
        private set => _currentSink.Value = value;
    }

    /// <summary>
    /// Gets or sets the current correlation ID for this async context.
    /// </summary>
    public static string? CorrelationId
    {
        get => _currentCorrelationId.Value;
        private set => _currentCorrelationId.Value = value;
    }

    /// <summary>
    /// Gets or sets the current phase context for this async context.
    /// </summary>
    public static string? PhaseContext
    {
        get => _currentPhaseContext.Value;
        private set => _currentPhaseContext.Value = value;
    }

    /// <summary>
    /// Sets the current event sink context.
    /// </summary>
    public static void Set(IToolEventSink? sink, string? correlationId = null, string? phaseContext = null)
    {
        Current = sink;
        CorrelationId = correlationId;
        PhaseContext = phaseContext;
    }

    /// <summary>
    /// Clears the current event sink context.
    /// </summary>
    public static void Clear()
    {
        Current = null;
        CorrelationId = null;
        PhaseContext = null;
    }

    /// <summary>
    /// Executes an action within the context of a specific event sink.
    /// Automatically clears the context after execution.
    /// </summary>
    public static async Task<T> ExecuteWithContextAsync<T>(
        IToolEventSink sink,
        string? correlationId,
        string? phaseContext,
        Func<Task<T>> action)
    {
        Set(sink, correlationId, phaseContext);
        try
        {
            return await action();
        }
        finally
        {
            Clear();
        }
    }

    /// <summary>
    /// Executes an action within the context of a specific event sink (void return).
    /// Automatically clears the context after execution.
    /// </summary>
    public static async Task ExecuteWithContextAsync(
        IToolEventSink sink,
        string? correlationId,
        string? phaseContext,
        Func<Task> action)
    {
        Set(sink, correlationId, phaseContext);
        try
        {
            await action();
        }
        finally
        {
            Clear();
        }
    }
}

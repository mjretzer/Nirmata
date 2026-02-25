using System.Diagnostics;
using Gmsd.Aos.Contracts.Tools;
using Microsoft.SemanticKernel;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Semantic Kernel function invocation filter that emits tool.call and tool.result events.
/// Injects IToolEventSink into function arguments so ToolToKernelFunctionAdapter can emit events.
/// </summary>
public sealed class StreamingToolEventFilter : IFunctionInvocationFilter
{
    private readonly IToolEventSink _eventSink;
    private readonly string? _correlationId;
    private readonly string? _phaseContext;

    /// <summary>
    /// Creates a new streaming tool event filter.
    /// </summary>
    /// <param name="eventSink">The tool event sink for emitting tool events</param>
    /// <param name="correlationId">Optional correlation ID for the operation</param>
    /// <param name="phaseContext">Optional phase context (e.g., "Executor", "Planner")</param>
    public StreamingToolEventFilter(
        IToolEventSink eventSink,
        string? correlationId = null,
        string? phaseContext = null)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _correlationId = correlationId;
        _phaseContext = phaseContext;
    }

    /// <inheritdoc />
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        // Inject event sink into arguments if not already present
        if (!context.Arguments.ContainsKey("EventSink"))
        {
            context.Arguments["EventSink"] = _eventSink;
        }

        if (!string.IsNullOrEmpty(_correlationId) && !context.Arguments.ContainsKey("CorrelationId"))
        {
            context.Arguments["CorrelationId"] = _correlationId;
        }

        if (!string.IsNullOrEmpty(_phaseContext) && !context.Arguments.ContainsKey("PhaseContext"))
        {
            context.Arguments["PhaseContext"] = _phaseContext;
        }

        // Proceed with the function invocation
        // The ToolToKernelFunctionAdapter will emit the actual tool.call and tool.result events
        await next(context);
    }
}

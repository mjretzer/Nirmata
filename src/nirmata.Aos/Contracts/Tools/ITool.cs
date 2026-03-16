namespace nirmata.Aos.Contracts.Tools;

/// <summary>
/// Core interface for all tool implementations in the engine.
/// Provides a unified contract for tool invocation with consistent request/result shapes.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Invokes the tool with the given request and context.
    /// </summary>
    /// <param name="request">The tool request containing input parameters.</param>
    /// <param name="context">The execution context for correlation and tracing.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous tool invocation with the result.</returns>
    Task<ToolResult> InvokeAsync(
        ToolRequest request,
        ToolContext context,
        CancellationToken cancellationToken = default);
}

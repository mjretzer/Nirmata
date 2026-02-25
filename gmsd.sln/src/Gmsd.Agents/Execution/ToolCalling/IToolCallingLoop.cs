namespace Gmsd.Agents.Execution.ToolCalling;

/// <summary>
/// Interface defining the multi-step conversation protocol for tool calling.
/// Manages the iterative cycle of sending tools to the LLM, executing tool calls,
/// and returning results until the conversation completes.
/// </summary>
public interface IToolCallingLoop
{
    /// <summary>
    /// Executes the tool calling conversation loop.
    /// </summary>
    /// <param name="request">The tool calling request containing messages, tools, and options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous execution with the final result.</returns>
    Task<ToolCallingResult> ExecuteAsync(
        ToolCallingRequest request,
        CancellationToken cancellationToken = default);
}

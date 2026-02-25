namespace Gmsd.Aos.Contracts.Tools;

/// <summary>
/// Interface for emitting tool execution events during tool invocation.
/// Implementations can stream tool.call and tool.result events to consumers.
/// </summary>
public interface IToolEventSink
{
    /// <summary>
    /// Emits a tool.call event before tool execution begins.
    /// </summary>
    /// <param name="callId">Unique identifier for correlating call with result</param>
    /// <param name="toolName">The name of the tool being invoked</param>
    /// <param name="parameters">Parameters passed to the tool</param>
    /// <param name="phaseContext">Phase context for the tool call</param>
    /// <param name="correlationId">Optional correlation ID</param>
    void EmitToolCall(
        string callId,
        string toolName,
        Dictionary<string, object>? parameters = null,
        string? phaseContext = null,
        string? correlationId = null);

    /// <summary>
    /// Emits a tool.result event after tool execution completes.
    /// </summary>
    /// <param name="callId">Unique identifier matching the original tool.call</param>
    /// <param name="success">Whether the tool execution succeeded</param>
    /// <param name="result">Result data from tool execution</param>
    /// <param name="error">Error message if execution failed</param>
    /// <param name="durationMs">Duration of tool execution in milliseconds</param>
    /// <param name="correlationId">Optional correlation ID</param>
    void EmitToolResult(
        string callId,
        bool success,
        object? result = null,
        string? error = null,
        long durationMs = 0,
        string? correlationId = null);
}

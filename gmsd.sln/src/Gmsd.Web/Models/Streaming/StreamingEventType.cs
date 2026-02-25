namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Defines the types of events that can be streamed from the orchestrator
/// </summary>
public enum StreamingEventType
{
    /// <summary>
    /// Command suggestion event when a natural language input is mapped to a command
    /// </summary>
    CommandSuggested,

    /// <summary>
    /// Event when user confirms a suggested command
    /// </summary>
    SuggestedCommandConfirmed,

    /// <summary>
    /// Event when user rejects/dismisses a suggested command
    /// </summary>
    SuggestedCommandRejected,

    /// <summary>
    /// Intent classification result with confidence and reasoning
    /// </summary>
    IntentClassified,

    /// <summary>
    /// Gating decision with selected phase and reasoning
    /// </summary>
    GateSelected,

    /// <summary>
    /// Tool invocation with parameters
    /// </summary>
    ToolCall,

    /// <summary>
    /// Tool execution result
    /// </summary>
    ToolResult,

    /// <summary>
    /// LLM requested tool calls (detected but not yet executed)
    /// </summary>
    ToolCallDetected,

    /// <summary>
    /// Individual tool execution began
    /// </summary>
    ToolCallStarted,

    /// <summary>
    /// Tool execution succeeded
    /// </summary>
    ToolCallCompleted,

    /// <summary>
    /// Tool execution failed
    /// </summary>
    ToolCallFailed,

    /// <summary>
    /// Tool results sent back to LLM
    /// </summary>
    ToolResultsSubmitted,

    /// <summary>
    /// One full iteration (LLM call + tool executions) completed
    /// </summary>
    ToolLoopIterationCompleted,

    /// <summary>
    /// Tool calling loop finished normally
    /// </summary>
    ToolLoopCompleted,

    /// <summary>
    /// Tool calling loop encountered an error
    /// </summary>
    ToolLoopFailed,

    /// <summary>
    /// Phase lifecycle event (started or completed)
    /// </summary>
    PhaseLifecycle,

    /// <summary>
    /// Assistant message delta (streaming token)
    /// </summary>
    AssistantDelta,

    /// <summary>
    /// Assistant message finalization
    /// </summary>
    AssistantFinal,

    /// <summary>
    /// Run lifecycle event (started or finished)
    /// </summary>
    RunLifecycle,

    /// <summary>
    /// Error event
    /// </summary>
    Error,

    /// <summary>
    /// UI navigation event for detail panel updates
    /// </summary>
    UiNavigation
}

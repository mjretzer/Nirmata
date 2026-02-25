namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Payload for command.suggested events
/// </summary>
public class CommandSuggestedPayload
{
    /// <summary>
    /// The name of the suggested command (e.g., "run", "plan", "status")
    /// </summary>
    public required string CommandName { get; set; }

    /// <summary>
    /// Arguments for the suggested command
    /// </summary>
    public string[]? Arguments { get; set; }

    /// <summary>
    /// The fully formatted command string
    /// </summary>
    public string? FormattedCommand { get; set; }

    /// <summary>
    /// Confidence score between 0.0 and 1.0
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reasoning or explanation for the suggestion
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// The original user input that triggered the suggestion
    /// </summary>
    public string? OriginalInput { get; set; }

    /// <summary>
    /// The confirmation request ID for tracking user response
    /// </summary>
    public string? ConfirmationRequestId { get; set; }
}

/// <summary>
/// Payload for suggested.command.confirmed events
/// </summary>
public class SuggestedCommandConfirmedPayload
{
    /// <summary>
    /// The confirmation request ID that was confirmed
    /// </summary>
    public required string ConfirmationRequestId { get; set; }

    /// <summary>
    /// The name of the command that was confirmed
    /// </summary>
    public required string CommandName { get; set; }

    /// <summary>
    /// The formatted command that will be executed
    /// </summary>
    public string? FormattedCommand { get; set; }

    /// <summary>
    /// The original user input that triggered the suggestion
    /// </summary>
    public string? OriginalInput { get; set; }

    /// <summary>
    /// Timestamp when the suggestion was initially made
    /// </summary>
    public DateTimeOffset? SuggestedAt { get; set; }

    /// <summary>
    /// Time taken for user to confirm (milliseconds)
    /// </summary>
    public long? DecisionTimeMs { get; set; }
}

/// <summary>
/// Payload for suggested.command.rejected events
/// </summary>
public class SuggestedCommandRejectedPayload
{
    /// <summary>
    /// The confirmation request ID that was rejected
    /// </summary>
    public required string ConfirmationRequestId { get; set; }

    /// <summary>
    /// The name of the command that was rejected
    /// </summary>
    public required string CommandName { get; set; }

    /// <summary>
    /// The formatted command that was rejected
    /// </summary>
    public string? FormattedCommand { get; set; }

    /// <summary>
    /// The original user input that triggered the suggestion
    /// </summary>
    public string? OriginalInput { get; set; }

    /// <summary>
    /// Optional reason provided by user for rejection
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Timestamp when the suggestion was initially made
    /// </summary>
    public DateTimeOffset? SuggestedAt { get; set; }

    /// <summary>
    /// Time taken for user to reject (milliseconds)
    /// </summary>
    public long? DecisionTimeMs { get; set; }

    /// <summary>
    /// Whether to continue as chat after rejection
    /// </summary>
    public bool ContinueAsChat { get; set; } = true;
}

/// <summary>
/// Payload for intent.classified events
/// </summary>
public class IntentClassifiedPayload
{
    /// <summary>
    /// The classified intent category (e.g., "Chat", "Write", "Plan", "Execute")
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Confidence score between 0.0 and 1.0
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reasoning or explanation for the classification decision
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// The original user input that was classified
    /// </summary>
    public string? UserInput { get; set; }
}

/// <summary>
/// Payload for gate.selected events
/// </summary>
public class GateSelectedPayload
{
    /// <summary>
    /// The selected target phase
    /// </summary>
    public required string Phase { get; set; }

    /// <summary>
    /// Reasoning for the gating decision
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// Whether this action requires user confirmation
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// The proposed action details if confirmation is required
    /// </summary>
    public ProposedAction? ProposedAction { get; set; }
}

/// <summary>
/// Represents a proposed action for confirmation flows
/// </summary>
public class ProposedAction
{
    /// <summary>
    /// Unique identifier for this proposed action
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Human-readable description of the action
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// The type of action being proposed
    /// </summary>
    public string? ActionType { get; set; }

    /// <summary>
    /// Additional context or parameters for the action
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Payload for tool.call events
/// </summary>
public class ToolCallPayload
{
    /// <summary>
    /// Unique identifier for correlating call with result
    /// </summary>
    public required string CallId { get; set; }

    /// <summary>
    /// The name of the tool being invoked
    /// </summary>
    public required string ToolName { get; set; }

    /// <summary>
    /// The parameters passed to the tool
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// The phase context in which the tool is being called
    /// </summary>
    public string? PhaseContext { get; set; }
}

/// <summary>
/// Payload for tool.result events
/// </summary>
public class ToolResultPayload
{
    /// <summary>
    /// Unique identifier matching the original tool.call
    /// </summary>
    public required string CallId { get; set; }

    /// <summary>
    /// Whether the tool execution succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The result data from tool execution
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Duration of tool execution in milliseconds
    /// </summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// Payload for phase.lifecycle events
/// </summary>
public class PhaseLifecyclePayload
{
    /// <summary>
    /// The phase name
    /// </summary>
    public required string Phase { get; set; }

    /// <summary>
    /// Whether this is a start or completion event
    /// </summary>
    public required string Status { get; set; } // "started" or "completed"

    /// <summary>
    /// Contextual information about the phase
    /// </summary>
    public Dictionary<string, object>? Context { get; set; }

    /// <summary>
    /// Output artifacts produced by the phase (for completed events)
    /// </summary>
    public List<PhaseArtifact>? Artifacts { get; set; }

    /// <summary>
    /// Error information if phase failed
    /// </summary>
    public PhaseError? Error { get; set; }
}

/// <summary>
/// Represents an artifact produced by a phase
/// </summary>
public class PhaseArtifact
{
    /// <summary>
    /// The type of artifact
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Human-readable name of the artifact
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Reference to access the artifact (e.g., path, ID)
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Summary or preview of the artifact content
    /// </summary>
    public string? Summary { get; set; }
}

/// <summary>
/// Represents a phase execution error
/// </summary>
public class PhaseError
{
    /// <summary>
    /// Error code or type
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Additional error details or stack trace
    /// </summary>
    public string? Details { get; set; }
}

/// <summary>
/// Payload for assistant.delta events (streaming tokens)
/// </summary>
public class AssistantDeltaPayload
{
    /// <summary>
    /// Unique identifier for the message being streamed
    /// </summary>
    public required string MessageId { get; set; }

    /// <summary>
    /// The token chunk content
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Position in the overall message (0-indexed)
    /// </summary>
    public int Index { get; set; }
}

/// <summary>
/// Payload for assistant.final events
/// </summary>
public class AssistantFinalPayload
{
    /// <summary>
    /// Unique identifier matching the delta stream
    /// </summary>
    public required string MessageId { get; set; }

    /// <summary>
    /// The complete final content
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Structured data if the response contains rich content
    /// </summary>
    public object? StructuredData { get; set; }

    /// <summary>
    /// The content type of the response
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Whether this response completes a streaming sequence
    /// </summary>
    public bool IsFinal { get; set; } = true;
}

/// <summary>
/// Payload for run.lifecycle events
/// </summary>
public class RunLifecyclePayload
{
    /// <summary>
    /// The run status: "started" or "finished"
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Unique identifier for the run
    /// </summary>
    public string? RunId { get; set; }

    /// <summary>
    /// Run duration in milliseconds (for finished events)
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Whether the run completed successfully (for finished events)
    /// </summary>
    public bool? Success { get; set; }

    /// <summary>
    /// References to artifacts produced by the run
    /// </summary>
    public List<string>? ArtifactReferences { get; set; }
}

/// <summary>
/// Payload for tool.call.detected events (Task 8.1)
/// Emitted when LLM requests tool calls
/// </summary>
public class ToolCallDetectedPayload
{
    /// <summary>
    /// The iteration number in the loop (1-indexed)
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// The tool calls requested by the LLM
    /// </summary>
    public List<ToolCallInfo> ToolCalls { get; set; } = new();
}

/// <summary>
/// Information about a detected tool call
/// </summary>
public class ToolCallInfo
{
    /// <summary>
    /// Unique identifier for this tool call
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the tool being called
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Arguments as a JSON string
    /// </summary>
    public string ArgumentsJson { get; set; } = string.Empty;
}

/// <summary>
/// Payload for tool.call.started events (Task 8.1)
/// Emitted when individual tool execution begins
/// </summary>
public class ToolCallStartedPayload
{
    /// <summary>
    /// The iteration number in the loop
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Unique identifier for this tool call
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the tool being executed
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Arguments being passed to the tool
    /// </summary>
    public string ArgumentsJson { get; set; } = string.Empty;
}

/// <summary>
/// Payload for tool.call.completed events (Task 8.1)
/// Emitted when tool execution succeeds
/// </summary>
public class ToolCallCompletedPayload
{
    /// <summary>
    /// The iteration number in the loop
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Unique identifier for this tool call
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the tool that was executed
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Duration of the tool execution in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Indicates if there is result content
    /// </summary>
    public bool HasResult { get; set; }

    /// <summary>
    /// Summary or preview of the result
    /// </summary>
    public string? ResultSummary { get; set; }
}

/// <summary>
/// Payload for tool.call.failed events (Task 8.1)
/// Emitted when tool execution fails
/// </summary>
public class ToolCallFailedPayload
{
    /// <summary>
    /// The iteration number in the loop
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Unique identifier for this tool call
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the tool that failed
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Error code
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Duration of the tool execution before failure in milliseconds
    /// </summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// Payload for tool.results.submitted events (Task 8.1)
/// Emitted when tool results are sent back to the LLM
/// </summary>
public class ToolResultsSubmittedPayload
{
    /// <summary>
    /// The iteration number in the loop
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Number of tool results being submitted
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// Information about the submitted results
    /// </summary>
    public List<ToolResultInfo> Results { get; set; } = new();
}

/// <summary>
/// Information about a submitted tool result
/// </summary>
public class ToolResultInfo
{
    /// <summary>
    /// Unique identifier for this tool call
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the tool
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the tool execution succeeded
    /// </summary>
    public bool IsSuccess { get; set; }
}

/// <summary>
/// Payload for tool.loop.iteration.completed events (Task 8.1)
/// Emitted when one full iteration completes
/// </summary>
public class ToolLoopIterationCompletedPayload
{
    /// <summary>
    /// The iteration number that completed
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Whether the LLM requested more tool calls in this iteration
    /// </summary>
    public bool HasMoreToolCalls { get; set; }

    /// <summary>
    /// Number of tool calls executed in this iteration
    /// </summary>
    public int ToolCallCount { get; set; }

    /// <summary>
    /// Duration of the iteration in milliseconds
    /// </summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// Payload for tool.loop.completed events (Task 8.1)
/// Emitted when tool calling loop finishes normally
/// </summary>
public class ToolLoopCompletedPayload
{
    /// <summary>
    /// Total number of iterations performed
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// Total number of tool calls executed across all iterations
    /// </summary>
    public int TotalToolCalls { get; set; }

    /// <summary>
    /// The reason the loop completed
    /// </summary>
    public string CompletionReason { get; set; } = string.Empty;

    /// <summary>
    /// Total duration of the loop in milliseconds
    /// </summary>
    public long TotalDurationMs { get; set; }
}

/// <summary>
/// Payload for tool.loop.failed events (Task 8.1)
/// Emitted when tool calling loop encounters an error
/// </summary>
public class ToolLoopFailedPayload
{
    /// <summary>
    /// Error code
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// The iteration when the error occurred
    /// </summary>
    public int Iteration { get; set; }
}

/// <summary>
/// Payload for ui.navigation events
/// </summary>
public class UiNavigationPayload
{
    /// <summary>
    /// The type of navigation action
    /// </summary>
    public required string Action { get; set; } // "showEntity", "showPage", "navigate"

    /// <summary>
    /// The entity type (for showEntity action)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// The entity ID (for showEntity action)
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// The page URL or path (for showPage action)
    /// </summary>
    public string? PageUrl { get; set; }

    /// <summary>
    /// The page title to display
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether to open in detail panel (true) or navigate away (false)
    /// </summary>
    public bool UseDetailPanel { get; set; } = true;

    /// <summary>
    /// Additional context data for the navigation
    /// </summary>
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// Payload for error events
/// </summary>
public class ErrorPayload
{
    /// <summary>
    /// Error severity level
    /// </summary>
    public required string Severity { get; set; } // "error", "warning", "info"

    /// <summary>
    /// Error code for categorization
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// The phase or component where the error occurred
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Whether the error is recoverable
    /// </summary>
    public bool Recoverable { get; set; }

    /// <summary>
    /// Suggested retry action if recoverable
    /// </summary>
    public string? RetryAction { get; set; }
}

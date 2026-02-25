/**
 * @fileoverview Event Renderer Type Definitions
 * @description JSDoc type definitions for streaming dialogue events
 * These mirror the C# StreamingEvent models in Gmsd.Web/Models/Streaming
 */

/**
 * @typedef {string} StreamingEventType
 * @readonly
 * @enum {string}
 */
const StreamingEventType = {
    IntentClassified: 'intent.classified',
    GateSelected: 'gate.selected',
    ToolCall: 'tool.call',
    ToolResult: 'tool.result',
    ToolCallDetected: 'tool.call.detected',
    ToolCallStarted: 'tool.call.started',
    ToolCallCompleted: 'tool.call.completed',
    ToolCallFailed: 'tool.call.failed',
    ToolResultsSubmitted: 'tool.results.submitted',
    ToolLoopIterationCompleted: 'tool.loop.iteration.completed',
    ToolLoopCompleted: 'tool.loop.completed',
    ToolLoopFailed: 'tool.loop.failed',
    PhaseLifecycle: 'phase.lifecycle',
    AssistantDelta: 'assistant.delta',
    AssistantFinal: 'assistant.final',
    RunLifecycle: 'run.lifecycle',
    ConfirmationRequested: 'confirmation.requested',
    ConfirmationAccepted: 'confirmation.accepted',
    ConfirmationRejected: 'confirmation.rejected',
    ConfirmationTimeout: 'confirmation.timeout',
    UiNavigation: 'ui.navigation',
    Error: 'error'
};

/**
 * @typedef {Object} StreamingEvent
 * @property {string} id - Unique event identifier (UUID)
 * @property {StreamingEventType} type - Event type discriminator
 * @property {string} timestamp - ISO 8601 timestamp
 * @property {string} [correlationId] - Links events across a conversation
 * @property {number} [sequenceNumber] - Ordering sequence (optional)
 * @property {Object} [payload] - Type-specific event data
 */

/**
 * @typedef {Object} IntentClassifiedPayload
 * @property {'Chat'|'ReadOnly'|'Write'} classification - Intent category
 * @property {number} confidence - Confidence score (0.0 - 1.0)
 * @property {string} reasoning - LLM explanation of classification
 */

/**
 * @typedef {Object} GateSelectedPayload
 * @property {string} phase - Selected target phase
 * @property {string} reasoning - Explanation for selection
 * @property {boolean} requiresConfirmation - Whether user confirmation needed
 * @property {ProposedAction} [proposedAction] - Action to confirm (if applicable)
 */

/**
 * @typedef {Object} ConfirmationRequestedPayload
 * @property {string} confirmationId - Unique confirmation identifier
 * @property {ProposedAction} action - Action requiring confirmation
 * @property {string} riskLevel - Risk level ('WriteSafe', 'WriteDestructive', etc.)
 * @property {string} reason - Human-readable reason for confirmation
 * @property {number} confidence - Confidence score
 * @property {number} [threshold] - Threshold that triggered confirmation
 * @property {string} [timeout] - ISO 8601 duration for timeout
 * @property {string} [confirmationKey] - Key for duplicate detection
 */

/**
 * @typedef {Object} ConfirmationAcceptedPayload
 * @property {string} confirmationId - Confirmation identifier
 * @property {string} acceptedAt - ISO 8601 timestamp
 * @property {ProposedAction} [action] - Action that was accepted
 */

/**
 * @typedef {Object} ConfirmationRejectedPayload
 * @property {string} confirmationId - Confirmation identifier
 * @property {string} rejectedAt - ISO 8601 timestamp
 * @property {string} [userMessage] - Optional user explanation
 * @property {ProposedAction} [action] - Action that was rejected
 */

/**
 * @typedef {Object} ConfirmationTimeoutPayload
 * @property {string} confirmationId - Confirmation identifier
 * @property {string} requestedAt - ISO 8601 timestamp when requested
 * @property {string} timeout - ISO 8601 duration that was exceeded
 * @property {ProposedAction} [action] - Action that timed out
 * @property {string} [cancellationReason] - Reason for cancellation
 * @property {string} message - Human-readable timeout message
 */

/**
 * @typedef {Object} ProposedAction
 * @property {string} name - Action name
 * @property {string} description - Action description
 * @property {Object} [parameters] - Action parameters
 */

/**
 * @typedef {Object} ToolCallPayload
 * @property {string} toolName - Name of tool being invoked
 * @property {Object} arguments - Tool arguments
 * @property {string} callId - Unique call identifier for correlation
 */

/**
 * @typedef {Object} ToolResultPayload
 * @property {string} callId - Matches the tool.call event
 * @property {boolean} success - Whether execution succeeded
 * @property {*} result - Tool execution result
 * @property {number} durationMs - Execution duration in milliseconds
 */

/**
 * @typedef {Object} ToolCallDetectedPayload
 * @property {number} iteration - The iteration number in the loop (1-indexed)
 * @property {Array<ToolCallInfo>} toolCalls - The tool calls requested by the LLM
 */

/**
 * @typedef {Object} ToolCallInfo
 * @property {string} toolCallId - Unique identifier for this tool call
 * @property {string} toolName - Name of the tool being called
 * @property {string} argumentsJson - Arguments as a JSON string
 */

/**
 * @typedef {Object} ToolCallStartedPayload
 * @property {number} iteration - The iteration number in the loop
 * @property {string} toolCallId - Unique identifier for this tool call
 * @property {string} toolName - Name of the tool being executed
 * @property {string} argumentsJson - Arguments being passed to the tool
 */

/**
 * @typedef {Object} ToolCallCompletedPayload
 * @property {number} iteration - The iteration number in the loop
 * @property {string} toolCallId - Unique identifier for this tool call
 * @property {string} toolName - Name of the tool that was executed
 * @property {number} durationMs - Duration of the tool execution in milliseconds
 * @property {boolean} hasResult - Indicates if there is result content
 * @property {string} [resultSummary] - Summary or preview of the result
 */

/**
 * @typedef {Object} ToolCallFailedPayload
 * @property {number} iteration - The iteration number in the loop
 * @property {string} toolCallId - Unique identifier for this tool call
 * @property {string} toolName - Name of the tool that failed
 * @property {string} errorCode - Error code
 * @property {string} errorMessage - Error message
 * @property {number} durationMs - Duration of the tool execution before failure in milliseconds
 */

/**
 * @typedef {Object} ToolResultsSubmittedPayload
 * @property {number} iteration - The iteration number in the loop
 * @property {number} resultCount - Number of tool results being submitted
 * @property {Array<ToolResultInfo>} results - Information about the submitted results
 */

/**
 * @typedef {Object} ToolResultInfo
 * @property {string} toolCallId - Unique identifier for this tool call
 * @property {string} toolName - Name of the tool
 * @property {boolean} isSuccess - Whether the tool execution succeeded
 */

/**
 * @typedef {Object} ToolLoopIterationCompletedPayload
 * @property {number} iteration - The iteration number that completed
 * @property {boolean} hasMoreToolCalls - Whether the LLM requested more tool calls in this iteration
 * @property {number} toolCallCount - Number of tool calls executed in this iteration
 * @property {number} durationMs - Duration of the iteration in milliseconds
 */

/**
 * @typedef {Object} ToolLoopCompletedPayload
 * @property {number} totalIterations - Total number of iterations performed
 * @property {number} totalToolCalls - Total number of tool calls executed across all iterations
 * @property {string} completionReason - The reason the loop completed
 * @property {number} totalDurationMs - Total duration of the loop in milliseconds
 */

/**
 * @typedef {Object} ToolLoopFailedPayload
 * @property {string} errorCode - Error code
 * @property {string} errorMessage - Error message
 * @property {number} iteration - The iteration when the error occurred
 */

/**
 * @typedef {Object} PhaseLifecyclePayload
 * @property {'started'|'completed'} status - Lifecycle status
 * @property {string} phase - Phase name
 * @property {string} [runId] - Associated run identifier
 * @property {Object} [context] - Phase context data
 * @property {Object} [outputArtifacts] - Output from completed phase
 */

/**
 * @typedef {Object} AssistantDeltaPayload
 * @property {string} content - Token chunk content
 * @property {string} messageId - Groups deltas into complete message
 */

/**
 * @typedef {Object} AssistantFinalPayload
 * @property {string} messageId - Matches delta events
 * @property {string} content - Complete message content
 * @property {*} [structuredData] - Optional parsed artifacts
 */

/**
 * @typedef {Object} RunLifecyclePayload
 * @property {'started'|'finished'} status - Lifecycle status
 * @property {string} runId - Run identifier
 * @property {string} [duration] - ISO 8601 duration string
 * @property {Object} [artifactReferences] - References to created artifacts
 */

/**
 * @typedef {Object} UiNavigationPayload
 * @property {string} action - The type of navigation action ('showEntity', 'showPage', 'navigate')
 * @property {string} [entityType] - The entity type (for showEntity action)
 * @property {string} [entityId] - The entity ID (for showEntity action)
 * @property {string} [pageUrl] - The page URL or path (for showPage action)
 * @property {string} [title] - The page title to display
 * @property {boolean} [useDetailPanel=true] - Whether to open in detail panel
 * @property {Object} [context] - Additional context data for the navigation
 */

/**
 * @typedef {Object} ErrorPayload
 * @property {string} code - Error code
 * @property {string} message - Error message
 * @property {string} [phase] - Phase context where error occurred
 * @property {string} [context] - General context where error occurred (alternative to phase)
 * @property {string} [severity='error'] - Error severity level ('error', 'warning', 'info')
 * @property {boolean} [recoverable=false] - Whether the error is recoverable
 * @property {string} [retryAction] - Suggested retry action if recoverable
 * @property {string} [eventId] - Associated event ID for retry correlation
 */

/**
 * Event category enumeration for grouping events by purpose
 * @readonly
 * @enum {string}
 */
const EventCategory = {
    /** Reasoning events - show agent's decision process */
    Reasoning: 'reasoning',
    /** Operation events - show work being performed */
    Operation: 'operation',
    /** Dialogue events - conversational content */
    Dialogue: 'dialogue',
    /** Error events - error conditions */
    Error: 'error',
    /** Unknown/uncategorized events */
    Unknown: 'unknown'
};

/**
 * Maps event types to their categories
 * @type {Object<StreamingEventType, EventCategory>}
 */
const EventTypeToCategory = {
    [StreamingEventType.IntentClassified]: EventCategory.Reasoning,
    [StreamingEventType.GateSelected]: EventCategory.Reasoning,
    [StreamingEventType.ToolCall]: EventCategory.Operation,
    [StreamingEventType.ToolResult]: EventCategory.Operation,
    [StreamingEventType.ToolCallDetected]: EventCategory.Reasoning,
    [StreamingEventType.ToolCallStarted]: EventCategory.Operation,
    [StreamingEventType.ToolCallCompleted]: EventCategory.Operation,
    [StreamingEventType.ToolCallFailed]: EventCategory.Error,
    [StreamingEventType.ToolResultsSubmitted]: EventCategory.Operation,
    [StreamingEventType.ToolLoopIterationCompleted]: EventCategory.Operation,
    [StreamingEventType.ToolLoopCompleted]: EventCategory.Operation,
    [StreamingEventType.ToolLoopFailed]: EventCategory.Error,
    [StreamingEventType.PhaseLifecycle]: EventCategory.Operation,
    [StreamingEventType.RunLifecycle]: EventCategory.Operation,
    [StreamingEventType.AssistantDelta]: EventCategory.Dialogue,
    [StreamingEventType.AssistantFinal]: EventCategory.Dialogue,
    [StreamingEventType.ConfirmationRequested]: EventCategory.Reasoning,
    [StreamingEventType.ConfirmationAccepted]: EventCategory.Operation,
    [StreamingEventType.ConfirmationRejected]: EventCategory.Operation,
    [StreamingEventType.ConfirmationTimeout]: EventCategory.Error,
    [StreamingEventType.UiNavigation]: EventCategory.Operation,
    [StreamingEventType.Error]: EventCategory.Error
};

/**
 * Gets the category for a given event type
 * @param {StreamingEventType} eventType
 * @returns {EventCategory}
 */
function getEventCategory(eventType) {
    return EventTypeToCategory[eventType] || EventCategory.Unknown;
}

// Export for module systems (CommonJS/Node.js testing)
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        StreamingEventType,
        EventCategory,
        EventTypeToCategory,
        getEventCategory
    };
}

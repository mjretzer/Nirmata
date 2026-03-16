using nirmata.Agents.Execution.Preflight;

namespace nirmata.Agents.Execution.ControlPlane.Streaming;

/// <summary>
/// Confirmation-specific SSE event types for streaming confirmation requests and responses.
/// </summary>
public enum ConfirmationEventType
{
    /// <summary>
    /// A confirmation has been requested from the user.
    /// </summary>
    ConfirmationRequested,

    /// <summary>
    /// User has accepted the confirmation request.
    /// </summary>
    ConfirmationAccepted,

    /// <summary>
    /// User has rejected the confirmation request.
    /// </summary>
    ConfirmationRejected,

    /// <summary>
    /// User has responded to a confirmation request (legacy - use Accepted/Rejected).
    /// </summary>
    ConfirmationResponded,

    /// <summary>
    /// A confirmation request has timed out.
    /// </summary>
    ConfirmationTimeout
}

/// <summary>
/// Base class for all confirmation streaming events.
/// </summary>
public abstract class ConfirmationEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The type of confirmation event.
    /// </summary>
    public ConfirmationEventType EventType { get; init; }

    /// <summary>
    /// ISO 8601 timestamp when the event was emitted.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing events across a conversation.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The confirmation ID this event relates to.
    /// </summary>
    public required string ConfirmationId { get; init; }
}

/// <summary>
/// Event emitted when a confirmation is requested from the user.
/// Corresponds to the UI rendering a confirmation dialog.
/// </summary>
public sealed class ConfirmationRequestedEvent : ConfirmationEvent
{
    public ConfirmationRequestedEvent()
    {
        EventType = ConfirmationEventType.ConfirmationRequested;
    }

    /// <summary>
    /// The proposed action requiring confirmation.
    /// </summary>
    public required ProposedAction Action { get; init; }

    /// <summary>
    /// The risk level of the operation.
    /// </summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Human-readable reason why confirmation is required.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Optional timeout for the confirmation.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// The confidence score that triggered the confirmation.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// The threshold that was not met (if applicable).
    /// </summary>
    public double? Threshold { get; init; }

    /// <summary>
    /// Unique hash key for duplicate detection.
    /// </summary>
    public string? ConfirmationKey { get; init; }
}

/// <summary>
/// Event emitted when a user accepts a confirmation request.
/// </summary>
public sealed class ConfirmationAcceptedEvent : ConfirmationEvent
{
    public ConfirmationAcceptedEvent()
    {
        EventType = ConfirmationEventType.ConfirmationAccepted;
    }

    /// <summary>
    /// When the confirmation was accepted.
    /// </summary>
    public DateTimeOffset AcceptedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The proposed action that was accepted.
    /// </summary>
    public ProposedAction? Action { get; init; }
}

/// <summary>
/// Event emitted when a user rejects a confirmation request.
/// </summary>
public sealed class ConfirmationRejectedEvent : ConfirmationEvent
{
    public ConfirmationRejectedEvent()
    {
        EventType = ConfirmationEventType.ConfirmationRejected;
    }

    /// <summary>
    /// When the confirmation was rejected.
    /// </summary>
    public DateTimeOffset RejectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional user message explaining the rejection.
    /// </summary>
    public string? UserMessage { get; init; }

    /// <summary>
    /// The proposed action that was rejected.
    /// </summary>
    public ProposedAction? Action { get; init; }
}

/// <summary>
/// Event emitted when a user responds to a confirmation request.
/// </summary>
public sealed class ConfirmationRespondedEvent : ConfirmationEvent
{
    public ConfirmationRespondedEvent()
    {
        EventType = ConfirmationEventType.ConfirmationResponded;
    }

    /// <summary>
    /// Whether the user accepted the confirmation.
    /// </summary>
    public required bool Accepted { get; init; }

    /// <summary>
    /// Optional user message accompanying the response.
    /// </summary>
    public string? UserMessage { get; init; }

    /// <summary>
    /// When the response was received.
    /// </summary>
    public DateTimeOffset RespondedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event emitted when a confirmation request times out.
/// </summary>
public sealed class ConfirmationTimeoutEvent : ConfirmationEvent
{
    public ConfirmationTimeoutEvent()
    {
        EventType = ConfirmationEventType.ConfirmationTimeout;
    }

    /// <summary>
    /// When the confirmation was originally requested.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; }

    /// <summary>
    /// The timeout duration that was exceeded.
    /// </summary>
    public required TimeSpan Timeout { get; init; }

    /// <summary>
    /// The proposed action that timed out.
    /// </summary>
    public ProposedAction? Action { get; init; }

    /// <summary>
    /// The reason for the cancellation (e.g., "timeout", "user_cancelled", "system_error").
    /// </summary>
    public string? CancellationReason { get; init; }

    /// <summary>
    /// Human-readable message explaining the timeout.
    /// </summary>
    public string Message => $"Confirmation timed out after {Timeout.TotalSeconds} seconds. The operation was cancelled{(CancellationReason != null ? $" due to: {CancellationReason}" : "")}.";
}

/// <summary>
/// Event emitted when a prerequisite is missing and conversational recovery is offered.
/// </summary>
public sealed class PrerequisiteMissingEvent : ConfirmationEvent
{
    public PrerequisiteMissingEvent()
    {
        EventType = ConfirmationEventType.ConfirmationRequested;
        // Using ConfirmationRequested type as this triggers a UI prompt
    }

    /// <summary>
    /// The missing prerequisite details.
    /// </summary>
    public required MissingPrerequisite MissingPrerequisite { get; init; }

    /// <summary>
    /// The recovery action suggested to the user.
    /// </summary>
    public string? RecoveryAction => MissingPrerequisite.RecoveryAction;

    /// <summary>
    /// The conversational prompt to present to the user.
    /// </summary>
    public string? ConversationalPrompt => MissingPrerequisite.ConversationalPrompt;
}

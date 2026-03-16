using nirmata.Agents.Execution.Preflight;

namespace nirmata.Agents.Execution.ControlPlane.Streaming;

/// <summary>
/// Interface for emitting streaming events to subscribers (UI, logs, etc.).
/// </summary>
public interface IStreamingEventEmitter
{
    /// <summary>
    /// Emits a chat streaming event.
    /// </summary>
    /// <param name="event">The event to emit.</param>
    void Emit(ChatStreamingEvent @event);

    /// <summary>
    /// Emits a confirmation event.
    /// </summary>
    /// <param name="event">The confirmation event to emit.</param>
    void Emit(ConfirmationEvent @event);

    /// <summary>
    /// Sets the correlation ID for subsequent events.
    /// </summary>
    /// <param name="correlationId">The correlation ID to use.</param>
    void SetCorrelationId(string correlationId);
}

/// <summary>
/// Publishes confirmation events through the streaming event emitter.
/// </summary>
public sealed class ConfirmationEventPublisher
{
    private readonly IStreamingEventEmitter _emitter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationEventPublisher"/> class.
    /// </summary>
    /// <param name="emitter">The streaming event emitter.</param>
    public ConfirmationEventPublisher(IStreamingEventEmitter emitter)
    {
        _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary>
    /// Publishes a confirmation requested event.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <param name="action">The proposed action.</param>
    /// <param name="riskLevel">The risk level.</param>
    /// <param name="reason">The reason for confirmation.</param>
    /// <param name="confidence">The confidence score.</param>
    /// <param name="threshold">The threshold that triggered confirmation.</param>
    /// <param name="timeout">Optional timeout.</param>
    public void PublishRequested(
        string confirmationId,
        ProposedAction action,
        RiskLevel riskLevel,
        string reason,
        double confidence,
        double? threshold = null,
        TimeSpan? timeout = null)
    {
        var @event = new ConfirmationRequestedEvent
        {
            ConfirmationId = confirmationId,
            Action = action,
            RiskLevel = riskLevel,
            Reason = reason,
            Confidence = confidence,
            Threshold = threshold,
            Timeout = timeout,
            ConfirmationKey = ComputeConfirmationKey(action)
        };

        _emitter.Emit(@event);
    }

    /// <summary>
    /// Publishes a confirmation responded event (legacy - emits Accepted/Rejected events).
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <param name="accepted">Whether the confirmation was accepted.</param>
    /// <param name="userMessage">Optional user message.</param>
    /// <param name="action">The proposed action that was responded to.</param>
    public void PublishResponded(string confirmationId, bool accepted, string? userMessage = null, ProposedAction? action = null)
    {
        if (accepted)
        {
            PublishAccepted(confirmationId, action);
        }
        else
        {
            PublishRejected(confirmationId, userMessage, action);
        }
    }

    /// <summary>
    /// Publishes a confirmation accepted event.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <param name="action">The proposed action that was accepted.</param>
    public void PublishAccepted(string confirmationId, ProposedAction? action = null)
    {
        var @event = new ConfirmationAcceptedEvent
        {
            ConfirmationId = confirmationId,
            AcceptedAt = DateTimeOffset.UtcNow,
            Action = action
        };

        _emitter.Emit(@event);
    }

    /// <summary>
    /// Publishes a confirmation rejected event.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <param name="userMessage">Optional user message explaining rejection.</param>
    /// <param name="action">The proposed action that was rejected.</param>
    public void PublishRejected(string confirmationId, string? userMessage = null, ProposedAction? action = null)
    {
        var @event = new ConfirmationRejectedEvent
        {
            ConfirmationId = confirmationId,
            RejectedAt = DateTimeOffset.UtcNow,
            UserMessage = userMessage,
            Action = action
        };

        _emitter.Emit(@event);
    }

    /// <summary>
    /// Publishes a confirmation timeout event.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <param name="requestedAt">When the confirmation was requested.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="action">The proposed action that timed out.</param>
    /// <param name="cancellationReason">The reason for cancellation.</param>
    public void PublishTimeout(
        string confirmationId,
        DateTimeOffset requestedAt,
        TimeSpan timeout,
        ProposedAction? action = null,
        string? cancellationReason = null)
    {
        var @event = new ConfirmationTimeoutEvent
        {
            ConfirmationId = confirmationId,
            RequestedAt = requestedAt,
            Timeout = timeout,
            Action = action,
            CancellationReason = cancellationReason ?? "timeout"
        };

        _emitter.Emit(@event);
    }

    /// <summary>
    /// Publishes a prerequisite missing event for conversational recovery.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <param name="missingPrerequisite">The missing prerequisite details.</param>
    public void PublishPrerequisiteMissing(string confirmationId, MissingPrerequisite missingPrerequisite)
    {
        var @event = new PrerequisiteMissingEvent
        {
            ConfirmationId = confirmationId,
            MissingPrerequisite = missingPrerequisite
        };

        _emitter.Emit(@event);
    }

    private static string ComputeConfirmationKey(ProposedAction action)
    {
        // Simple hash of phase + affected resources for duplicate detection
        var key = $"{action.Phase}:{string.Join(",", action.AffectedResources)}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key));
    }
}

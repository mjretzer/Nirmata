using System.Text.Json.Serialization;

namespace Gmsd.Aos.Contracts.State;

/// <summary>
/// Represents the state of a pending confirmation stored in .aos/state/confirmations.json.
/// </summary>
public sealed record ConfirmationState
{
    /// <summary>
    /// Unique identifier for this confirmation.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Current state of the confirmation (pending, accepted, rejected, timed_out).
    /// </summary>
    [JsonPropertyName("status")]
    public ConfirmationStatus Status { get; init; } = ConfirmationStatus.Pending;

    /// <summary>
    /// When the confirmation was requested.
    /// </summary>
    [JsonPropertyName("requestedAt")]
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional timeout duration. If null, confirmation waits indefinitely.
    /// </summary>
    [JsonPropertyName("timeout")]
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// When the confirmation expires (calculated from RequestedAt + Timeout).
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// The proposed action requiring confirmation.
    /// </summary>
    [JsonPropertyName("action")]
    public required ProposedAction Action { get; init; }

    /// <summary>
    /// The risk level of the operation.
    /// </summary>
    [JsonPropertyName("riskLevel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Human-readable reason why confirmation is required.
    /// </summary>
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }

    /// <summary>
    /// The confidence score that triggered the confirmation.
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    /// <summary>
    /// The threshold that was not met (if applicable).
    /// </summary>
    [JsonPropertyName("threshold")]
    public double? Threshold { get; init; }

    /// <summary>
    /// Unique hash key for duplicate detection.
    /// </summary>
    [JsonPropertyName("confirmationKey")]
    public string? ConfirmationKey { get; init; }

    /// <summary>
    /// When the confirmation was responded to (if accepted or rejected).
    /// </summary>
    [JsonPropertyName("respondedAt")]
    public DateTimeOffset? RespondedAt { get; init; }

    /// <summary>
    /// Optional user message when rejected.
    /// </summary>
    [JsonPropertyName("userMessage")]
    public string? UserMessage { get; init; }

    /// <summary>
    /// When the confirmation timed out (if timed_out status).
    /// </summary>
    [JsonPropertyName("timedOutAt")]
    public DateTimeOffset? TimedOutAt { get; init; }

    /// <summary>
    /// The reason for cancellation when timed out.
    /// </summary>
    [JsonPropertyName("cancellationReason")]
    public string? CancellationReason { get; init; }

    /// <summary>
    /// Correlation ID for linking events across a conversation.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Checks if this confirmation has expired.
    /// </summary>
    public bool IsExpired()
    {
        if (!ExpiresAt.HasValue)
        {
            return false;
        }
        return DateTimeOffset.UtcNow > ExpiresAt.Value;
    }

    /// <summary>
    /// Gets the remaining time before expiration.
    /// </summary>
    public TimeSpan? GetRemainingTime()
    {
        if (!ExpiresAt.HasValue)
        {
            return null;
        }
        var remaining = ExpiresAt.Value - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}

/// <summary>
/// Status values for a confirmation state.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfirmationStatus
{
    /// <summary>
    /// Confirmation is pending user response.
    /// </summary>
    Pending,

    /// <summary>
    /// User has accepted the confirmation.
    /// </summary>
    Accepted,

    /// <summary>
    /// User has rejected the confirmation.
    /// </summary>
    Rejected,

    /// <summary>
    /// Confirmation has timed out.
    /// </summary>
    TimedOut
}

/// <summary>
/// Container for all confirmations stored in .aos/state/confirmations.json.
/// </summary>
public sealed class ConfirmationsStateDocument
{
    /// <summary>
    /// Schema version for migrations.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// All confirmations (pending and resolved).
    /// </summary>
    [JsonPropertyName("confirmations")]
    public List<ConfirmationState> Confirmations { get; init; } = new();
}

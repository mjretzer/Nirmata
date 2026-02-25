namespace Gmsd.Agents.Execution.ControlPlane.Chat;

using Models;

/// <summary>
/// Manages the confirmation flow for suggested commands.
/// </summary>
public sealed class CommandConfirmationFlow
{
    /// <summary>
    /// Represents a confirmation request for a suggested command.
    /// </summary>
    public sealed class ConfirmationRequest
    {
        /// <summary>
        /// Unique identifier for this confirmation request.
        /// </summary>
        public required string ConfirmationId { get; init; }

        /// <summary>
        /// The suggested command to confirm.
        /// </summary>
        public required string CommandName { get; init; }

        /// <summary>
        /// Arguments for the command.
        /// </summary>
        public required Dictionary<string, string> Arguments { get; init; }

        /// <summary>
        /// User-friendly message explaining the suggestion.
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Confidence score for the suggestion.
        /// </summary>
        public double Confidence { get; init; }

        /// <summary>
        /// Timestamp when the confirmation was requested.
        /// </summary>
        public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Timeout for the confirmation request.
        /// </summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Represents a user's response to a confirmation request.
    /// </summary>
    public sealed class ConfirmationResponse
    {
        /// <summary>
        /// The confirmation request ID being responded to.
        /// </summary>
        public required string ConfirmationId { get; init; }

        /// <summary>
        /// Whether the user accepted the suggestion.
        /// </summary>
        public required bool Accepted { get; init; }

        /// <summary>
        /// Optional user feedback or modifications.
        /// </summary>
        public string? UserFeedback { get; init; }

        /// <summary>
        /// Timestamp when the response was provided.
        /// </summary>
        public DateTimeOffset RespondedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Stores pending confirmation requests.
    /// </summary>
    private readonly Dictionary<string, ConfirmationRequest> _pendingConfirmations = new();

    /// <summary>
    /// Creates a new confirmation request for a suggested command.
    /// </summary>
    public ConfirmationRequest CreateConfirmation(string commandName, Dictionary<string, string> arguments, string message, double confidence)
    {
        var confirmationId = Guid.NewGuid().ToString("N");
        
        var request = new ConfirmationRequest
        {
            ConfirmationId = confirmationId,
            CommandName = commandName,
            Arguments = arguments,
            Message = message,
            Confidence = confidence
        };

        _pendingConfirmations[confirmationId] = request;
        return request;
    }

    /// <summary>
    /// Retrieves a pending confirmation request.
    /// </summary>
    public ConfirmationRequest? GetPendingConfirmation(string confirmationId)
    {
        if (_pendingConfirmations.TryGetValue(confirmationId, out var request))
        {
            // Check if the request has timed out
            if (DateTimeOffset.UtcNow - request.RequestedAt > request.Timeout)
            {
                _pendingConfirmations.Remove(confirmationId);
                return null;
            }

            return request;
        }

        return null;
    }

    /// <summary>
    /// Processes a user's response to a confirmation request.
    /// </summary>
    public ConfirmationResponse? ProcessResponse(string confirmationId, bool accepted, string? userFeedback = null)
    {
        if (!_pendingConfirmations.TryGetValue(confirmationId, out var request))
        {
            return null;
        }

        _pendingConfirmations.Remove(confirmationId);

        return new ConfirmationResponse
        {
            ConfirmationId = confirmationId,
            Accepted = accepted,
            UserFeedback = userFeedback
        };
    }

    /// <summary>
    /// Cleans up expired confirmation requests.
    /// </summary>
    public void CleanupExpiredConfirmations()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredIds = _pendingConfirmations
            .Where(kvp => now - kvp.Value.RequestedAt > kvp.Value.Timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in expiredIds)
        {
            _pendingConfirmations.Remove(id);
        }
    }

    /// <summary>
    /// Gets all pending confirmation requests.
    /// </summary>
    public IReadOnlyList<ConfirmationRequest> GetAllPendingConfirmations()
    {
        CleanupExpiredConfirmations();
        return _pendingConfirmations.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Formats a confirmation request as a user-friendly message.
    /// </summary>
    public static string FormatConfirmationMessage(ConfirmationRequest request)
    {
        var argString = request.Arguments.Count > 0
            ? $" with arguments: {string.Join(", ", request.Arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"))}"
            : string.Empty;

        return $"Would you like me to execute `/{request.CommandName}`{argString}? {request.Message}";
    }
}

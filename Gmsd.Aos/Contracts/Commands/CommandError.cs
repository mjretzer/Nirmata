namespace Gmsd.Aos.Contracts.Commands;

/// <summary>
/// Represents a structured command error.
/// </summary>
public sealed class CommandError
{
    /// <summary>
    /// Error code identifying the type of error.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Additional error details, if any.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Creates a new command error.
    /// </summary>
    public CommandError(string code, string message)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }
}

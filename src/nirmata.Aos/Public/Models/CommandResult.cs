using nirmata.Aos.Contracts.Commands;

namespace nirmata.Aos.Public.Models;

/// <summary>
/// Base result returned from command execution.
/// </summary>
public sealed class CommandResult
{
    /// <summary>
    /// Whether the command executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The exit code (0 for success, non-zero for failure).
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Standard output from the command.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Error output from the command.
    /// </summary>
    public string? ErrorOutput { get; init; }

    /// <summary>
    /// Structured errors, if any.
    /// </summary>
    public IReadOnlyList<CommandError> Errors { get; init; } = Array.Empty<CommandError>();

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static CommandResult Success(string? output = null) => new()
    {
        IsSuccess = true,
        ExitCode = 0,
        Output = output
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static CommandResult Failure(int exitCode, string? errorOutput = null, IReadOnlyList<CommandError>? errors = null) => new()
    {
        IsSuccess = false,
        ExitCode = exitCode,
        ErrorOutput = errorOutput,
        Errors = errors ?? Array.Empty<CommandError>()
    };

    /// <summary>
    /// Creates a failure result with a single error message.
    /// </summary>
    public static CommandResult Failure(string errorMessage, int exitCode = 1) => new()
    {
        IsSuccess = false,
        ExitCode = exitCode,
        ErrorOutput = errorMessage,
        Errors = new[] { new CommandError("CommandFailed", errorMessage) }
    };

    /// <summary>
    /// Converts to a CommandRouteResult for public surface compatibility.
    /// </summary>
    public CommandRouteResult ToRouteResult() => new()
    {
        IsSuccess = IsSuccess,
        ExitCode = ExitCode,
        Output = Output,
        ErrorOutput = ErrorOutput,
        Errors = Errors
    };
}

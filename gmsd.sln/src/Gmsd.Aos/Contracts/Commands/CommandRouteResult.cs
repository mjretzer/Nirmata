namespace Gmsd.Aos.Contracts.Commands;

/// <summary>
/// Represents the result of routing a command request.
/// </summary>
public sealed class CommandRouteResult
{
    /// <summary>
    /// Whether the command was found and executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The exit code (0 for success, non-zero for failure).
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Standard output from the command execution.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Error output from the command execution.
    /// </summary>
    public string? ErrorOutput { get; init; }

    /// <summary>
    /// Routing hint for the orchestrator (e.g., "FixPlanner", "continue").
    /// </summary>
    public string? RoutingHint { get; init; }

    /// <summary>
    /// Structured errors, if any.
    /// </summary>
    public IReadOnlyList<CommandError> Errors { get; init; } = Array.Empty<CommandError>();

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static CommandRouteResult Success(string? output = null) => new()
    {
        IsSuccess = true,
        ExitCode = 0,
        Output = output
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static CommandRouteResult Failure(int exitCode, string? errorOutput = null, IReadOnlyList<CommandError>? errors = null) => new()
    {
        IsSuccess = false,
        ExitCode = exitCode,
        ErrorOutput = errorOutput,
        Errors = errors ?? Array.Empty<CommandError>()
    };

    /// <summary>
    /// Creates a result for an unknown command.
    /// </summary>
    public static CommandRouteResult UnknownCommand(string group, string command) => new()
    {
        IsSuccess = false,
        ExitCode = 1,
        ErrorOutput = $"Unknown command: {group} {command}",
        Errors = new[] { new CommandError("UnknownCommand", $"No handler registered for '{group} {command}'") }
    };
}

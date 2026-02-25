namespace Gmsd.Aos.Contracts.Commands;

/// <summary>
/// Represents a command request with group, command name, and arguments.
/// </summary>
public sealed class CommandRequest
{
    /// <summary>
    /// The command group (e.g., "spec", "state", "run").
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// The command name within the group (e.g., "init", "validate", "status").
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Command arguments, if any.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Named options/flags for the command.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Options { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Creates a simple command request without arguments.
    /// </summary>
    public static CommandRequest Create(string group, string command) =>
        new() { Group = group, Command = command };
}

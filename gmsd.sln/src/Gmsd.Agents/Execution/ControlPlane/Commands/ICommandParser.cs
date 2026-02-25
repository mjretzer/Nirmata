namespace Gmsd.Agents.Execution.ControlPlane.Commands;

/// <summary>
/// Represents a parsed command with its name and arguments.
/// </summary>
public class ParsedCommand
{
    public required string CommandName { get; init; }
    public required Dictionary<string, object?> Arguments { get; init; }
    public required string RawInput { get; init; }
}

/// <summary>
/// Parses user input to extract slash commands with strict prefix rules.
/// </summary>
public interface ICommandParser
{
    /// <summary>
    /// Attempts to parse a command from the input string.
    /// </summary>
    /// <param name="input">The raw user input.</param>
    /// <returns>A ParsedCommand if the input starts with '/', null otherwise.</returns>
    ParsedCommand? TryParseCommand(string input);

    /// <summary>
    /// Checks if the input appears to be a command (starts with '/').
    /// </summary>
    bool IsCommand(string input);
}

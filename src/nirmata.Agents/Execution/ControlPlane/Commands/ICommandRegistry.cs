namespace nirmata.Agents.Execution.ControlPlane.Commands;

/// <summary>
/// Metadata about a registered command.
/// </summary>
public class CommandMetadata
{
    public required string Name { get; init; }
    public required string HelpText { get; init; }
    public required Dictionary<string, ArgumentSchema> ArgumentSchemas { get; init; }
}

/// <summary>
/// Schema for a command argument.
/// </summary>
public class ArgumentSchema
{
    public required string Name { get; init; }
    public required ArgumentType Type { get; init; }
    public bool Required { get; init; }
    public string? Description { get; init; }
    public List<string>? AllowedValues { get; init; }
}

/// <summary>
/// Supported argument types for basic validation.
/// </summary>
public enum ArgumentType
{
    String,
    Integer,
    Boolean,
    Path
}

/// <summary>
/// Registry for managing command metadata, help text, and argument schemas.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>
    /// Registers a command with its metadata.
    /// </summary>
    void RegisterCommand(CommandMetadata metadata);

    /// <summary>
    /// Gets metadata for a registered command.
    /// </summary>
    CommandMetadata? GetCommand(string commandName);

    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    IReadOnlyList<CommandMetadata> GetAllCommands();

    /// <summary>
    /// Validates parsed arguments against the command's schema.
    /// </summary>
    /// <returns>A list of validation errors, empty if valid.</returns>
    List<string> ValidateArguments(string commandName, Dictionary<string, object?> arguments);
}

namespace nirmata.Aos.Contracts.Commands;

/// <summary>
/// Metadata describing a registered command.
/// </summary>
public sealed class CommandMetadata
{
    /// <summary>
    /// The command group (e.g., "spec", "state", "run").
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// The command name within the group.
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Human-readable description of the command.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Command identifier constant from CommandIds.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Example usage, if applicable.
    /// </summary>
    public string? Example { get; init; }

    /// <summary>
    /// Gets the full command key (group:command).
    /// </summary>
    public string Key => $"{Group}:{Command}";
}

using nirmata.Aos.Contracts.Commands;

using nirmata.Aos.Public.Models;

namespace nirmata.Aos.Public.Catalogs;

/// <summary>
/// Catalog for registering and resolving command handlers.
/// </summary>
public sealed class CommandCatalog
{
    private readonly Dictionary<string, CommandRegistration> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a command handler.
    /// </summary>
    public void Register(CommandMetadata metadata, Func<CommandContext, Task<CommandResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(handler);

        var key = $"{metadata.Group}:{metadata.Command}";
        if (_handlers.ContainsKey(key))
        {
            throw new InvalidOperationException($"Command '{key}' is already registered.");
        }

        _handlers[key] = new CommandRegistration(metadata, handler);
    }

    /// <summary>
    /// Attempts to resolve a handler for the given command.
    /// </summary>
    public bool TryResolve(string group, string command, out CommandRegistration? registration)
    {
        var key = $"{group}:{command}";
        return _handlers.TryGetValue(key, out registration);
    }

    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    public IEnumerable<CommandMetadata> GetAllCommands() =>
        _handlers.Values.Select(r => r.Metadata).OrderBy(m => m.Group).ThenBy(m => m.Command);

    /// <summary>
    /// Gets commands for a specific group.
    /// </summary>
    public IEnumerable<CommandMetadata> GetCommandsByGroup(string group) =>
        _handlers.Values
            .Where(r => r.Metadata.Group.Equals(group, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Metadata)
            .OrderBy(m => m.Command);

    /// <summary>
    /// Represents a registered command with its handler.
    /// </summary>
    public sealed class CommandRegistration
    {
        public CommandMetadata Metadata { get; }
        public Func<CommandContext, Task<CommandResult>> Handler { get; }

        public CommandRegistration(CommandMetadata metadata, Func<CommandContext, Task<CommandResult>> handler)
        {
            Metadata = metadata;
            Handler = handler;
        }
    }
}

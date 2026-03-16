namespace nirmata.Agents.Execution.Preflight;

/// <summary>
/// Defines a registered command with its metadata and side effect classification.
/// </summary>
public sealed class CommandRegistration
{
    /// <summary>
    /// The command name (e.g., "run", "plan", "status")
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The command group/category (e.g., "workflow", "query", "chat")
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// The side effect level of this command
    /// </summary>
    public required SideEffect SideEffect { get; init; }

    /// <summary>
    /// Human-readable description of what the command does
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Example usage of the command
    /// </summary>
    public string? Example { get; init; }

    /// <summary>
    /// Whether this command requires confirmation before execution
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Aliases for this command (e.g., "exec" for "execute")
    /// </summary>
    public string[] Aliases { get; init; } = [];
}

/// <summary>
/// Registry of supported commands with their side effect mappings.
/// Provides command lookup and validation functionality.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    IEnumerable<CommandRegistration> GetAllCommands();

    /// <summary>
    /// Gets a command by its name or alias.
    /// </summary>
    /// <param name="name">The command name or alias to look up.</param>
    /// <returns>The command registration if found, otherwise null.</returns>
    CommandRegistration? GetCommand(string name);

    /// <summary>
    /// Determines if a command name is registered.
    /// </summary>
    bool IsKnownCommand(string name);

    /// <summary>
    /// Gets commands by their side effect level.
    /// </summary>
    IEnumerable<CommandRegistration> GetCommandsBySideEffect(SideEffect sideEffect);

    /// <summary>
    /// Gets suggestions for unknown commands based on similarity.
    /// </summary>
    IEnumerable<string> GetSuggestions(string unknownCommand, int maxSuggestions = 3);
}

/// <summary>
/// Default implementation of the command registry with built-in commands.
/// </summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliasToCommand = new(StringComparer.OrdinalIgnoreCase);

    public CommandRegistry()
    {
        RegisterDefaultCommands();
    }

    private void RegisterDefaultCommands()
    {
        // Write commands (require confirmation for ambiguous cases)
        Register(new CommandRegistration
        {
            Name = "run",
            Group = "workflow",
            SideEffect = SideEffect.Write,
            Description = "Execute the current task or workflow",
            Example = "/run",
            RequiresConfirmation = false
        });

        Register(new CommandRegistration
        {
            Name = "plan",
            Group = "workflow",
            SideEffect = SideEffect.Write,
            Description = "Create or update a plan for the current phase",
            Example = "/plan",
            RequiresConfirmation = false
        });

        Register(new CommandRegistration
        {
            Name = "verify",
            Group = "workflow",
            SideEffect = SideEffect.Write,
            Description = "Verify the current task execution",
            Example = "/verify",
            RequiresConfirmation = false
        });

        Register(new CommandRegistration
        {
            Name = "fix",
            Group = "workflow",
            SideEffect = SideEffect.Write,
            Description = "Create a fix plan for identified issues",
            Example = "/fix",
            RequiresConfirmation = false
        });

        Register(new CommandRegistration
        {
            Name = "pause",
            Group = "workflow",
            SideEffect = SideEffect.Write,
            Description = "Pause the current workflow execution",
            Example = "/pause",
            RequiresConfirmation = false
        });

        Register(new CommandRegistration
        {
            Name = "resume",
            Group = "workflow",
            SideEffect = SideEffect.Write,
            Description = "Resume a paused workflow execution",
            Example = "/resume",
            RequiresConfirmation = false
        });

        // Read-only commands (no confirmation needed)
        Register(new CommandRegistration
        {
            Name = "status",
            Group = "query",
            SideEffect = SideEffect.ReadOnly,
            Description = "Check the current workflow status",
            Example = "/status",
            RequiresConfirmation = false
        });

        Register(new CommandRegistration
        {
            Name = "view",
            Group = "navigation",
            SideEffect = SideEffect.ReadOnly,
            Description = "View a page or entity in the detail panel",
            Example = "/view projects",
            RequiresConfirmation = false,
            Aliases = ["open", "show"]
        });

        Register(new CommandRegistration
        {
            Name = "help",
            Group = "query",
            SideEffect = SideEffect.ReadOnly,
            Description = "Show available commands and usage information",
            Example = "/help",
            RequiresConfirmation = false,
            Aliases = ["commands", "?"]
        });
    }

    private void Register(CommandRegistration command)
    {
        _commands[command.Name] = command;

        // Register aliases
        foreach (var alias in command.Aliases)
        {
            _aliasToCommand[alias] = command.Name;
        }
    }

    /// <inheritdoc />
    public IEnumerable<CommandRegistration> GetAllCommands()
    {
        return _commands.Values;
    }

    /// <inheritdoc />
    public CommandRegistration? GetCommand(string name)
    {
        if (_commands.TryGetValue(name, out var command))
        {
            return command;
        }

        // Check if it's an alias
        if (_aliasToCommand.TryGetValue(name, out var commandName))
        {
            return _commands.GetValueOrDefault(commandName);
        }

        return null;
    }

    /// <inheritdoc />
    public bool IsKnownCommand(string name)
    {
        return _commands.ContainsKey(name) || _aliasToCommand.ContainsKey(name);
    }

    /// <inheritdoc />
    public IEnumerable<CommandRegistration> GetCommandsBySideEffect(SideEffect sideEffect)
    {
        return _commands.Values.Where(c => c.SideEffect == sideEffect);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSuggestions(string unknownCommand, int maxSuggestions = 3)
    {
        var allCommandNames = _commands.Keys.Concat(_aliasToCommand.Keys).ToList();

        return allCommandNames
            .Select(name => new { Name = name, Distance = CalculateLevenshteinDistance(unknownCommand, name) })
            .OrderBy(x => x.Distance)
            .Take(maxSuggestions)
            .Select(x => x.Name);
    }

    private static int CalculateLevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var distances = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++)
            distances[i, 0] = i;

        for (var j = 0; j <= b.Length; j++)
            distances[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;

                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[a.Length, b.Length];
    }
}

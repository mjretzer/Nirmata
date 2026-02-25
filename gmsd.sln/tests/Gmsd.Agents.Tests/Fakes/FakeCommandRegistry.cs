using Gmsd.Agents.Execution.Preflight;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of ICommandRegistry for unit testing.
/// </summary>
public sealed class FakeCommandRegistry : ICommandRegistry
{
    private readonly List<CommandRegistration> _commands = new();

    /// <inheritdoc />
    public IEnumerable<CommandRegistration> GetAllCommands() => _commands;

    /// <inheritdoc />
    public CommandRegistration? GetCommand(string name) =>
        _commands.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public bool IsKnownCommand(string name) => GetCommand(name) != null;

    /// <inheritdoc />
    public IEnumerable<CommandRegistration> GetCommandsBySideEffect(SideEffect sideEffect) =>
        _commands.Where(c => c.SideEffect == sideEffect);

    /// <inheritdoc />
    public IEnumerable<string> GetSuggestions(string input, int maxSuggestions = 3) =>
        _commands
            .Where(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(maxSuggestions)
            .Select(c => c.Name);

    /// <summary>
    /// Adds a command to the registry.
    /// </summary>
    public void AddCommand(CommandRegistration command) => _commands.Add(command);

    /// <summary>
    /// Clears all commands from the registry.
    /// </summary>
    public void Clear() => _commands.Clear();
}

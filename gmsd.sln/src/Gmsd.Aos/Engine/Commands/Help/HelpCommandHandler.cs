using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public.Catalogs;

using Gmsd.Aos.Public.Models;
using Gmsd.Aos.Public.Services;

namespace Gmsd.Aos.Engine.Commands.Help;

/// <summary>
/// Handler for help command that generates help output from command catalog.
/// </summary>
internal sealed class HelpCommandHandler : ICommandHandler
{
    private readonly CommandCatalog _catalog;

    /// <summary>
    /// Creates a new help command handler.
    /// </summary>
    public HelpCommandHandler(CommandCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "help",
        Id = CommandIds.Help,
        Description = "Show help information for available commands."
    };

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("AOS Command Reference");
        sb.AppendLine("=====================");
        sb.AppendLine();

        var commands = _catalog.GetAllCommands().ToList();

        if (commands.Count == 0)
        {
            sb.AppendLine("No commands registered.");
        }
        else
        {
            var groups = commands.GroupBy(c => c.Group).OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                sb.AppendLine($"[{group.Key}]");
                foreach (var cmd in group)
                {
                    sb.AppendLine($"  {cmd.Command,-15} {cmd.Description}");
                }
                sb.AppendLine();
            }
        }

        return Task.FromResult(CommandResult.Success(sb.ToString().TrimEnd()));
    }
}

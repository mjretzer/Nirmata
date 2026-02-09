using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Engine.Commands.Base;
using Gmsd.Aos.Public.Catalogs;

namespace Gmsd.Aos.Engine.Commands.State;

/// <summary>
/// Handler for state commands.
/// </summary>
public sealed class StateCommandHandler : ICommandHandler
{
    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "state",
        Id = CommandIds.StateShow,
        Description = "Manage AOS state (show, diff)."
    };

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            if (context.Arguments.Count == 0)
            {
                return Task.FromResult(CommandResult.Failure(
                    "Usage: state <show|diff> [options]",
                    1
                ));
            }

            var subcommand = context.Arguments[0];
            return subcommand.ToLowerInvariant() switch
            {
                "show" => HandleShow(context),
                "diff" => HandleDiff(context),
                _ => Task.FromResult(CommandResult.Failure(
                    $"Unknown state subcommand: {subcommand}",
                    1
                ))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failure(
                $"State operation failed: {ex.Message}",
                1
            ));
        }
    }

    private static Task<CommandResult> HandleShow(CommandContext context)
    {
        var stateDir = Path.Combine(context.Workspace.AosRootPath, "state");
        if (!Directory.Exists(stateDir))
        {
            return Task.FromResult(CommandResult.Failure(
                "State directory does not exist. Run 'aos init' first.",
                1
            ));
        }

        // TODO: Implement actual state loading and display
        return Task.FromResult(CommandResult.Success("Current state: (not implemented)"));
    }

    private static Task<CommandResult> HandleDiff(CommandContext context)
    {
        // TODO: Implement actual state diff
        return Task.FromResult(CommandResult.Success("State diff: (not implemented)"));
    }
}

using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Engine.Commands.Base;
using Gmsd.Aos.Public.Catalogs;

namespace Gmsd.Aos.Engine.Commands.Spec;

/// <summary>
/// Handler for spec commands.
/// </summary>
public sealed class SpecCommandHandler : ICommandHandler
{
    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "spec",
        Id = CommandIds.SpecList,
        Description = "Manage AOS specifications (list, show, apply)."
    };

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            if (context.Arguments.Count == 0)
            {
                return Task.FromResult(CommandResult.Failure(
                    "Usage: spec <list|show|apply> [options]",
                    1
                ));
            }

            var subcommand = context.Arguments[0];
            return subcommand.ToLowerInvariant() switch
            {
                "list" => HandleList(context),
                "show" => HandleShow(context),
                "apply" => HandleApply(context),
                _ => Task.FromResult(CommandResult.Failure(
                    $"Unknown spec subcommand: {subcommand}",
                    1
                ))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failure(
                $"Spec operation failed: {ex.Message}",
                1
            ));
        }
    }

    private static Task<CommandResult> HandleList(CommandContext context)
    {
        var specDir = Path.Combine(context.Workspace.AosRootPath, "spec");
        if (!Directory.Exists(specDir))
        {
            return Task.FromResult(CommandResult.Failure(
                "Spec directory does not exist. Run 'aos init' first.",
                1
            ));
        }

        var specs = Directory.GetFiles(specDir, "*.md", SearchOption.AllDirectories);
        var output = specs.Length == 0
            ? "No specifications found."
            : "Specifications:\n" + string.Join("\n", specs.Select(s => $"  - {Path.GetRelativePath(specDir, s)}"));

        return Task.FromResult(CommandResult.Success(output));
    }

    private static Task<CommandResult> HandleShow(CommandContext context)
    {
        if (context.Arguments.Count < 2)
        {
            return Task.FromResult(CommandResult.Failure(
                "Usage: spec show <spec-name>",
                1
            ));
        }

        var specName = context.Arguments[1];
        // TODO: Implement actual spec loading and display
        return Task.FromResult(CommandResult.Success($"Showing spec: {specName} (not implemented)"));
    }

    private static Task<CommandResult> HandleApply(CommandContext context)
    {
        if (context.Arguments.Count < 2)
        {
            return Task.FromResult(CommandResult.Failure(
                "Usage: spec apply <spec-name>",
                1
            ));
        }

        var specName = context.Arguments[1];
        // TODO: Implement actual spec application
        return Task.FromResult(CommandResult.Success($"Applying spec: {specName} (not implemented)"));
    }
}

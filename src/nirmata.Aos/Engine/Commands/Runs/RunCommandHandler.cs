using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Engine.Evidence;
using nirmata.Aos.Public.Catalogs;

using nirmata.Aos.Public.Models;
using nirmata.Aos.Public.Services;

namespace nirmata.Aos.Engine.Commands.Runs;

/// <summary>
/// Handler for run commands.
/// </summary>
internal sealed class RunCommandHandler : ICommandHandler
{
    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "run",
        Id = CommandIds.RunExecute,
        Description = "Manage AOS runs (execute, list)."
    };

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            if (context.Arguments.Count == 0)
            {
                return Task.FromResult(CommandResult.Failure(
                    "Usage: run <execute|list> [options]",
                    1
                ));
            }

            var subcommand = context.Arguments[0];
            return subcommand.ToLowerInvariant() switch
            {
                "execute" => HandleExecute(context),
                "list" => HandleList(context),
                _ => Task.FromResult(CommandResult.Failure(
                    $"Unknown run subcommand: {subcommand}",
                    1
                ))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failure(
                $"Run operation failed: {ex.Message}",
                1
            ));
        }
    }

    private static Task<CommandResult> HandleExecute(CommandContext context)
    {
        var runId = AosRunId.New();

        // Scaffold run evidence structure
        AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
            context.Workspace.AosRootPath,
            runId,
            startedAtUtc: DateTimeOffset.UtcNow,
            command: "run execute",
            args: context.Arguments.Skip(1).ToArray()
        );

        var output = $"Run started: {runId}";
        return Task.FromResult(CommandResult.Success(output));
    }

    private static Task<CommandResult> HandleList(CommandContext context)
    {
        var runsDir = Path.Combine(context.Workspace.AosRootPath, "evidence", "runs");
        if (!Directory.Exists(runsDir))
        {
            return Task.FromResult(CommandResult.Success("No runs found."));
        }

        var runs = Directory.GetDirectories(runsDir)
            .Select(d => Path.GetFileName(d))
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderByDescending(name => name);

        var output = runs.Any()
            ? "Runs:\n" + string.Join("\n", runs.Select(r => $"  - {r}"))
            : "No runs found.";

        return Task.FromResult(CommandResult.Success(output));
    }
}

using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Engine.Commands.Base;
using Gmsd.Aos.Engine.Workspace;
using Gmsd.Aos.Public.Catalogs;

namespace Gmsd.Aos.Engine.Commands.Init;

/// <summary>
/// Handler for the init command.
/// </summary>
public sealed class InitCommandHandler : ICommandHandler
{
    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "init",
        Id = CommandIds.Init,
        Description = "Initialize an AOS workspace in the current directory."
    };

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            var result = AosWorkspaceBootstrapper.EnsureInitialized(context.Workspace.RepositoryRootPath);

            var output = $"{result.Outcome}: {result.AosRootPath}";
            return Task.FromResult(CommandResult.Success(output));
        }
        catch (AosWorkspaceNonCompliantException ex)
        {
            return Task.FromResult(CommandResult.Failure(
                1,
                ex.Message,
                new[] { new CommandError("WorkspaceNonCompliant", ex.Message) }
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failure(
                1,
                $"Init failed: {ex.Message}",
                new[] { new CommandError("InitFailed", ex.Message) }
            ));
        }
    }
}

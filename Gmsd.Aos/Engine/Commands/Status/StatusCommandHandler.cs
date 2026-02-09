using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Engine.Commands.Base;
using Gmsd.Aos.Engine.Workspace;
using Gmsd.Aos.Public.Catalogs;

namespace Gmsd.Aos.Engine.Commands.Status;

/// <summary>
/// Handler for the status command.
/// </summary>
public sealed class StatusCommandHandler : ICommandHandler
{
    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "status",
        Id = CommandIds.Status,
        Description = "Show the current status of the AOS workspace."
    };

    /// <inheritdoc />
    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            var aosRootPath = context.Workspace.AosRootPath;

            if (!Directory.Exists(aosRootPath))
            {
                return Task.FromResult(CommandResult.Failure(
                    "AOS workspace is not initialized. Run 'aos init' first.",
                    1
                ));
            }

            var compliance = AosWorkspaceBootstrapper.CheckCompliance(context.Workspace.RepositoryRootPath);

            if (!compliance.IsCompliant)
            {
                var issues = new List<string>();
                issues.AddRange(compliance.MissingDirectories.Select(d => $"Missing directory: {d}"));
                issues.AddRange(compliance.InvalidDirectories.Select(d => $"Invalid directory: {d}"));
                issues.AddRange(compliance.MissingFiles.Select(f => $"Missing file: {f}"));
                issues.AddRange(compliance.InvalidFiles.Select(f => $"Invalid file: {f}"));
                issues.AddRange(compliance.ExtraTopLevelEntries.Select(e => $"Extra entry: {e}"));

                var issuesText = issues.Count == 0
                    ? "Unknown compliance issues"
                    : string.Join("\n", issues.Select(i => $"  - {i}"));

                return Task.FromResult(CommandResult.Failure(
                    $"Workspace is non-compliant:\n{issuesText}",
                    1
                ));
            }

            var status = $"Workspace: {aosRootPath}\n" +
                        $"Compliant: Yes";

            return Task.FromResult(CommandResult.Success(status));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failure(
                $"Status check failed: {ex.Message}",
                1
            ));
        }
    }
}

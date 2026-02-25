using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Engine.Evidence.Commands;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Catalogs;
using Gmsd.Aos.Public.Services;

using Gmsd.Aos.Public.Models;

namespace Gmsd.Aos.Engine.Commands;

/// <summary>
/// Implementation of the command router that dispatches to registered handlers.
/// </summary>
internal sealed class CommandRouter : ICommandRouter
{
    private readonly CommandCatalog _catalog;
    private readonly IWorkspace _workspace;
    private readonly IEvidenceStore? _evidenceStore;

    /// <summary>
    /// Creates a new command router.
    /// </summary>
    public CommandRouter(CommandCatalog catalog, IWorkspace workspace, IEvidenceStore? evidenceStore = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _evidenceStore = evidenceStore;
    }

    /// <inheritdoc />
    public async Task<CommandRouteResult> RouteAsync(CommandRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Group))
        {
            return CommandRouteResult.Failure(1, "Command group is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return CommandRouteResult.Failure(1, "Command name is required.");
        }

        if (!_catalog.TryResolve(request.Group, request.Command, out var registration) || registration is null)
        {
            return CommandRouteResult.UnknownCommand(request.Group, request.Command);
        }

        var context = new CommandContext
        {
            Workspace = _workspace,
            EvidenceStore = _evidenceStore,
            CancellationToken = ct,
            Arguments = request.Arguments,
            Options = request.Options
        };

        CommandResult result;
        try
        {
            result = await registration.Handler(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = CommandResult.Failure(
                1,
                $"Command execution failed: {ex.Message}",
                new[] { new CommandError("ExecutionFailed", ex.Message) }
            );
        }

        // Write to evidence log if enabled
        WriteCommandEvidence(request, result);

        return result.ToRouteResult();
    }

    private void WriteCommandEvidence(CommandRequest request, CommandResult result)
    {
        try
        {
            var fullCommand = $"{request.Group} {request.Command}";
            var allArgs = request.Arguments.ToList();
            foreach (var option in request.Options)
            {
                allArgs.Add($"--{option.Key}");
                if (option.Value is not null)
                {
                    allArgs.Add(option.Value);
                }
            }

            // Try to determine run ID from context if available
            string? runId = null;
            if (_evidenceStore is not null)
            {
                // Run ID would be provided by the evidence store if within a run context
                // For now, we write to the global commands log
            }

            AosCommandLogWriter.AppendCommand(
                _workspace.AosRootPath,
                fullCommand,
                allArgs,
                result.ExitCode,
                runId
            );
        }
        catch
        {
            // Evidence writing is best-effort; don't fail the command if logging fails
        }
    }
}

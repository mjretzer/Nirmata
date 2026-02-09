using Gmsd.Aos.Contracts.Commands;

namespace Gmsd.Agents.Execution.Continuity;

/// <summary>
/// Command handler for the resume-task command.
/// Restores execution state from any historical RUN evidence folder.
/// </summary>
public sealed class ResumeTaskCommandHandler
{
    private readonly IPauseResumeManager _pauseResumeManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResumeTaskCommandHandler"/> class.
    /// </summary>
    public ResumeTaskCommandHandler(IPauseResumeManager pauseResumeManager)
    {
        _pauseResumeManager = pauseResumeManager ?? throw new ArgumentNullException(nameof(pauseResumeManager));
    }

    /// <summary>
    /// Handles the resume-task command.
    /// </summary>
    /// <param name="request">The command request containing run-id option.</param>
    /// <param name="runId">The current run identifier (not used for resume-task).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result indicating success or failure with new run ID.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string? runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Extract run-id from request
            var targetRunId = ExtractRunId(request);

            if (string.IsNullOrEmpty(targetRunId))
            {
                return CommandRouteResult.Failure(
                    1,
                    "Missing required argument: --run-id.\n" +
                    "Usage: resume-task --run-id=RUN-xxx or resume-task RUN-xxx");
            }

            // Validate run-id format
            if (!targetRunId.StartsWith("RUN-", StringComparison.OrdinalIgnoreCase))
            {
                return CommandRouteResult.Failure(
                    2,
                    $"Invalid run-id format: '{targetRunId}'. Run IDs must start with 'RUN-'.");
            }

            // Execute resume from run
            var resumeResult = await _pauseResumeManager.ResumeFromRunAsync(targetRunId, ct);

            return new CommandRouteResult
            {
                IsSuccess = true,
                Output = $"Task resumed from historical run.\n" +
                        $"  Historical run: {targetRunId}\n" +
                        $"  New run ID: {resumeResult.RunId}\n" +
                        $"  Current task: {resumeResult.Cursor.TaskId ?? "unknown"}\n" +
                        $"  Current phase: {resumeResult.Cursor.PhaseId ?? "unknown"}",
                RoutingHint = "Orchestrator"
            };
        }
        catch (DirectoryNotFoundException ex)
        {
            return CommandRouteResult.Failure(
                3,
                $"Run not found: {ex.Message}\n" +
                "Use 'list-runs' to see available runs.");
        }
        catch (InvalidDataException ex)
        {
            return CommandRouteResult.Failure(
                4,
                $"Run evidence is corrupted: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Resume-task operation failed: {ex.Message}");
        }
    }

    private static string? ExtractRunId(CommandRequest request)
    {
        // Try to extract from options
        if (request.Options.TryGetValue("run-id", out var runId) && !string.IsNullOrEmpty(runId))
        {
            return runId;
        }

        // Try to extract from arguments (e.g., --run-id=RUN-xxx)
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--run-id=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[9..].Trim('"', '\'');
            }
        }

        // Check first non-flag argument
        foreach (var arg in request.Arguments)
        {
            if (!arg.StartsWith("--"))
            {
                return arg;
            }
        }

        return null;
    }
}

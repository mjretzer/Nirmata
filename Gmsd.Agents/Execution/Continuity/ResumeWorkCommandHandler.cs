using Gmsd.Aos.Contracts.Commands;

namespace Gmsd.Agents.Execution.Continuity;

/// <summary>
/// Command handler for the resume-work command.
/// Reconstructs execution state from the handoff snapshot and resumes workflow.
/// </summary>
public sealed class ResumeWorkCommandHandler
{
    private readonly IPauseResumeManager _pauseResumeManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResumeWorkCommandHandler"/> class.
    /// </summary>
    public ResumeWorkCommandHandler(IPauseResumeManager pauseResumeManager)
    {
        _pauseResumeManager = pauseResumeManager ?? throw new ArgumentNullException(nameof(pauseResumeManager));
    }

    /// <summary>
    /// Handles the resume-work command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result indicating success or failure with new run ID.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string? runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Validate handoff before attempting resume
            var validationResult = await _pauseResumeManager.ValidateHandoffAsync(ct);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors);
                return CommandRouteResult.Failure(
                    1,
                    $"Cannot resume: handoff validation failed. {errors}\n" +
                    "Suggestion: Use 'start-run' to create a new run instead.");
            }

            // Execute resume
            var resumeResult = await _pauseResumeManager.ResumeAsync(ct);

            var statusMessage = resumeResult.Status switch
            {
                ResumeStatus.Success => "successfully",
                ResumeStatus.SuccessWithWarnings => "with warnings",
                ResumeStatus.Failed => "failed",
                _ => "completed"
            };

            return new CommandRouteResult
            {
                IsSuccess = resumeResult.Status != ResumeStatus.Failed,
                Output = $"Work resumed {statusMessage}.\n" +
                        $"  New run ID: {resumeResult.RunId}\n" +
                        $"  Source run: {resumeResult.SourceRunId}\n" +
                        $"  Current task: {resumeResult.Cursor.TaskId ?? "unknown"}\n" +
                        $"  Current phase: {resumeResult.Cursor.PhaseId ?? "unknown"}",
                RoutingHint = "Orchestrator" // Signal to continue with orchestration
            };
        }
        catch (FileNotFoundException)
        {
            return CommandRouteResult.Failure(
                2,
                "No handoff.json found. Cannot resume without a prior pause.\n" +
                "Suggestion: Use 'start-run' to create a new run instead.");
        }
        catch (InvalidDataException ex)
        {
            return CommandRouteResult.Failure(3, $"Handoff data is invalid: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Resume operation failed: {ex.Message}");
        }
    }
}

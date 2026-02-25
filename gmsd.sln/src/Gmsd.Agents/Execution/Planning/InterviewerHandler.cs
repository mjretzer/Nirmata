using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public.Services;

namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Command handler for the Interviewer phase of the orchestrator workflow.
/// </summary>
public sealed class InterviewerHandler
{
    private readonly INewProjectInterviewer _interviewer;
    private readonly IInterviewEvidenceWriter _evidenceWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterviewerHandler"/> class.
    /// </summary>
    public InterviewerHandler(
        INewProjectInterviewer interviewer,
        IInterviewEvidenceWriter evidenceWriter)
    {
        _interviewer = interviewer ?? throw new ArgumentNullException(nameof(interviewer));
        _evidenceWriter = evidenceWriter ?? throw new ArgumentNullException(nameof(evidenceWriter));
    }

    /// <summary>
    /// Handles the interviewer phase command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Create a new interview session
            var session = new InterviewSession
            {
                RunId = runId,
                State = InterviewState.NotStarted
            };

            // Conduct the interview
            var result = await _interviewer.ConductInterviewAsync(session, ct);

            if (result.Success)
            {
                return new CommandRouteResult
                {
                    IsSuccess = true,
                    Output = $"Interview completed successfully. Project spec generated for '{result.ProjectSpec?.Name}'. " +
                             $"Artifacts written: {string.Join(", ", result.Artifacts.Select(a => a.FileName))}"
                };
            }
            else
            {
                return new CommandRouteResult
                {
                    IsSuccess = false,
                    ErrorOutput = result.ErrorMessage ?? "Interview failed without specific error."
                };
            }
        }
        catch (Exception ex)
        {
            return new CommandRouteResult
            {
                IsSuccess = false,
                ErrorOutput = $"Interviewer handler failed: {ex.Message}"
            };
        }
    }
}

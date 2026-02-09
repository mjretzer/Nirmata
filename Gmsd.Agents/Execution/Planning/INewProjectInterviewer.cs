namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Defines the contract for conducting new project interviews to gather requirements.
/// </summary>
public interface INewProjectInterviewer
{
    /// <summary>
    /// Conducts an interview session to gather project requirements.
    /// </summary>
    /// <param name="session">The interview session containing current state and history.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the interview including the generated project spec.</returns>
    Task<InterviewResult> ConductInterviewAsync(InterviewSession session, CancellationToken ct = default);
}

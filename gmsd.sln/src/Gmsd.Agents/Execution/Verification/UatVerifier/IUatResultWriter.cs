namespace Gmsd.Agents.Execution.Verification.UatVerifier;

/// <summary>
/// Defines the contract for writing UAT result artifacts.
/// </summary>
public interface IUatResultWriter
{
    /// <summary>
    /// Writes the UAT verification result to the evidence store.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="result">The verification result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The path to the written artifact.</returns>
    Task<string> WriteResultAsync(string taskId, string runId, UatVerificationResult result, CancellationToken ct = default);
}

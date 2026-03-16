namespace nirmata.Agents.Execution.Verification.UatVerifier;

/// <summary>
/// Defines the contract for UAT verification services.
/// </summary>
public interface IUatVerifier
{
    /// <summary>
    /// Executes UAT verification against the provided request.
    /// </summary>
    /// <param name="request">The verification request containing acceptance criteria and context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The verification result with check outcomes.</returns>
    Task<UatVerificationResult> VerifyAsync(UatVerificationRequest request, CancellationToken ct = default);
}

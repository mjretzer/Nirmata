namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Provider-neutral interface for LLM completions.
/// Implementations translate normalized requests/responses to provider-specific formats.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Gets the provider name (e.g., "openai", "anthropic", "ollama").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sends a completion request to the LLM and returns the response.
    /// </summary>
    /// <param name="request">The completion request containing messages and options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous completion with the response.</returns>
    /// <exception cref="LlmProviderException">Thrown when the provider returns an error or the request fails.</exception>
    Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);
}

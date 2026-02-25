namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Provider-neutral interface for LLM completions.
/// Implementations translate normalized requests/responses to provider-specific formats.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService directly. " +
          "This abstraction will be removed in a future release.", false)]
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

    /// <summary>
    /// Streams a completion request to the LLM and yields delta chunks as they arrive.
    /// </summary>
    /// <param name="request">The completion request containing messages and options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of delta chunks containing streaming content.</returns>
    /// <exception cref="LlmProviderException">Thrown when the provider returns an error or the request fails.</exception>
    IAsyncEnumerable<LlmDelta> StreamCompletionAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);
}

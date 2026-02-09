using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.Ollama;

/// <summary>
/// LLM provider adapter for Ollama local LLM API.
/// </summary>
public sealed class OllamaLlmAdapter : ILlmProvider
{
    /// <inheritdoc />
    public string ProviderName => "ollama";

    /// <inheritdoc />
    public Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Scaffold implementation - to be completed
        throw new NotImplementedException("Ollama adapter not yet implemented.");
    }
}

using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.OpenAi;

/// <summary>
/// LLM provider adapter for OpenAI API.
/// </summary>
public sealed class OpenAiLlmAdapter : ILlmProvider
{
    /// <inheritdoc />
    public string ProviderName => "openai";

    /// <inheritdoc />
    public Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Scaffold implementation - to be completed
        throw new NotImplementedException("OpenAI adapter not yet implemented.");
    }
}

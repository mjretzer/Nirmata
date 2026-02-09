using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.AzureOpenAi;

/// <summary>
/// LLM provider adapter for Azure OpenAI Service API.
/// </summary>
public sealed class AzureOpenAiLlmAdapter : ILlmProvider
{
    /// <inheritdoc />
    public string ProviderName => "azure-openai";

    /// <inheritdoc />
    public Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Scaffold implementation - to be completed
        throw new NotImplementedException("Azure OpenAI adapter not yet implemented.");
    }
}

using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.Anthropic;

/// <summary>
/// LLM provider adapter for Anthropic Claude API.
/// </summary>
public sealed class AnthropicLlmAdapter : ILlmProvider
{
    /// <inheritdoc />
    public string ProviderName => "anthropic";

    /// <inheritdoc />
    public Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Scaffold implementation - to be completed
        throw new NotImplementedException("Anthropic adapter not yet implemented.");
    }
}

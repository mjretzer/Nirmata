namespace Gmsd.Agents.Execution.ControlPlane;

using Chat;
using Chat.Models;

/// <summary>
/// Chat responder that delegates to the LLM-backed IChatResponder implementation.
/// This class acts as a simple adapter for backward compatibility during the transition.
/// </summary>
public sealed class ChatResponder
{
    private readonly IChatResponder _chatResponder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatResponder"/> class.
    /// </summary>
    public ChatResponder(IChatResponder chatResponder)
    {
        _chatResponder = chatResponder ?? throw new ArgumentNullException(nameof(chatResponder));
    }

    /// <summary>
    /// Generates a chat response using the LLM provider.
    /// </summary>
    public async Task<OrchestratorResult> Respond(string input)
    {
        var request = new ChatRequest
        {
            Input = input,
            IncludeWorkspaceContext = true
        };

        var response = await _chatResponder.RespondAsync(request);

        return new OrchestratorResult
        {
            IsSuccess = response.IsSuccess,
            FinalPhase = "Chat",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = response.Content,
                ["model"] = response.Model ?? "unknown",
                ["promptTokens"] = response.PromptTokens,
                ["completionTokens"] = response.CompletionTokens,
                ["totalTokens"] = response.TotalTokens,
                ["durationMs"] = response.DurationMs,
                ["correlationId"] = response.CorrelationId ?? "none"
            }
        };
    }
}

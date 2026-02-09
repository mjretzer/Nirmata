namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Normalized request for LLM completion.
/// </summary>
public sealed record LlmCompletionRequest
{
    /// <summary>
    /// The conversation messages to send to the LLM.
    /// </summary>
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>
    /// Optional model identifier override. If null, the provider uses its configured default.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Provider options including temperature, max tokens, etc.
    /// </summary>
    public LlmProviderOptions Options { get; init; } = new();

    /// <summary>
    /// Tool definitions available for this completion request.
    /// </summary>
    public IReadOnlyList<LlmToolDefinition>? Tools { get; init; }

    /// <summary>
    /// When set, forces the model to use this specific tool.
    /// </summary>
    public string? ToolChoice { get; init; }
}

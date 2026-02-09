namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Options for configuring LLM completion behavior.
/// </summary>
public sealed record LlmProviderOptions
{
    /// <summary>
    /// Sampling temperature (0.0 to 2.0). Lower values are more deterministic.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Maximum number of tokens to generate.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Nucleus sampling parameter (0.0 to 1.0). Alternative to temperature.
    /// </summary>
    public float? TopP { get; init; }

    /// <summary>
    /// Penalize new tokens based on their frequency in the text so far.
    /// </summary>
    public float? FrequencyPenalty { get; init; }

    /// <summary>
    /// Penalize new tokens based on whether they appear in the text so far.
    /// </summary>
    public float? PresencePenalty { get; init; }

    /// <summary>
    /// Stop sequences that will halt generation when encountered.
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// Random seed for deterministic sampling (provider support varies).
    /// </summary>
    public int? Seed { get; init; }
}

namespace nirmata.Agents.Execution.Preflight.CommandSuggestion;

/// <summary>
/// Configuration options for command suggestion behavior.
/// </summary>
public sealed class CommandSuggestionOptions
{
    /// <summary>
    /// Whether command suggestion mode is enabled.
    /// When disabled, the system will not suggest commands for freeform input.
    /// </summary>
    public bool EnableSuggestionMode { get; init; } = true;

    /// <summary>
    /// Default confidence threshold for suggesting commands.
    /// Suggestions below this threshold will not be offered.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.7;

    /// <summary>
    /// Maximum length of user input to analyze for suggestions.
    /// Longer inputs may be truncated.
    /// </summary>
    public int MaxInputLength { get; init; } = 1000;

    /// <summary>
    /// Whether to include command examples in suggestion reasoning.
    /// </summary>
    public bool IncludeExamples { get; init; } = true;
}

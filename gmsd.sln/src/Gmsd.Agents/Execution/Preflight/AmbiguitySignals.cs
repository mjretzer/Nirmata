namespace Gmsd.Agents.Execution.Preflight;

/// <summary>
/// Represents the level of ambiguity detected in user input.
/// </summary>
public enum AmbiguityLevel
{
    /// <summary>
    /// No ambiguity detected - intent is clear.
    /// </summary>
    None,

    /// <summary>
    /// Low ambiguity - minor uncertainty but still actionable.
    /// </summary>
    Low,

    /// <summary>
    /// Medium ambiguity - significant uncertainty requiring confirmation.
    /// </summary>
    Medium,

    /// <summary>
    /// High ambiguity - very unclear intent, confirmation strongly recommended.
    /// </summary>
    High
}

/// <summary>
/// Signals detected during intent classification that indicate ambiguous or unclear user intent.
/// </summary>
public sealed class AmbiguitySignals
{
    /// <summary>
    /// Whether the input is considered ambiguous based on detected signals.
    /// </summary>
    public bool IsAmbiguous => Level != AmbiguityLevel.None;

    /// <summary>
    /// The overall ambiguity level based on combined signals.
    /// </summary>
    public AmbiguityLevel Level { get; init; } = AmbiguityLevel.None;

    /// <summary>
    /// Whether the confidence score is below the threshold (low confidence signal).
    /// </summary>
    public bool HasLowConfidence { get; init; }

    /// <summary>
    /// The confidence score that triggered the low confidence signal.
    /// </summary>
    public double ConfidenceScore { get; init; }

    /// <summary>
    /// Whether vague or ambiguous verbs were detected in the input.
    /// </summary>
    public bool HasVagueVerbs { get; init; }

    /// <summary>
    /// The vague verbs detected in the input, if any.
    /// </summary>
    public string[] DetectedVagueVerbs { get; init; } = [];

    /// <summary>
    /// Whether required context is missing for the intent.
    /// </summary>
    public bool HasMissingContext { get; init; }

    /// <summary>
    /// Description of what context is missing, if applicable.
    /// </summary>
    public string? MissingContextDescription { get; init; }

    /// <summary>
    /// Whether the input contains multiple conflicting intents.
    /// </summary>
    public bool HasConflictingIntents { get; init; }

    /// <summary>
    /// Human-readable explanation of why the input is ambiguous.
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// Creates signals indicating no ambiguity.
    /// </summary>
    public static AmbiguitySignals Clear(double confidenceScore)
    {
        return new AmbiguitySignals
        {
            Level = AmbiguityLevel.None,
            HasLowConfidence = false,
            ConfidenceScore = confidenceScore,
            HasVagueVerbs = false,
            HasMissingContext = false,
            HasConflictingIntents = false,
            Reasoning = "No ambiguity detected - intent is clear"
        };
    }

    /// <summary>
    /// Creates signals for low confidence ambiguity.
    /// </summary>
    public static AmbiguitySignals LowConfidence(double confidenceScore, double threshold)
    {
        return new AmbiguitySignals
        {
            Level = AmbiguityLevel.Medium,
            HasLowConfidence = true,
            ConfidenceScore = confidenceScore,
            HasVagueVerbs = false,
            HasMissingContext = false,
            HasConflictingIntents = false,
            Reasoning = $"Confidence ({confidenceScore:F2}) below threshold ({threshold:F2})"
        };
    }

    /// <summary>
    /// Creates signals for vague verbs ambiguity.
    /// </summary>
    public static AmbiguitySignals VagueVerbs(string[] detectedVerbs, double confidenceScore)
    {
        return new AmbiguitySignals
        {
            Level = AmbiguityLevel.Medium,
            HasLowConfidence = false,
            ConfidenceScore = confidenceScore,
            HasVagueVerbs = true,
            DetectedVagueVerbs = detectedVerbs,
            HasMissingContext = false,
            HasConflictingIntents = false,
            Reasoning = $"Detected vague verbs: {string.Join(", ", detectedVerbs)}"
        };
    }

    /// <summary>
    /// Creates signals for missing context ambiguity.
    /// </summary>
    public static AmbiguitySignals MissingContext(string contextDescription, double confidenceScore)
    {
        return new AmbiguitySignals
        {
            Level = AmbiguityLevel.High,
            HasLowConfidence = false,
            ConfidenceScore = confidenceScore,
            HasVagueVerbs = false,
            HasMissingContext = true,
            MissingContextDescription = contextDescription,
            HasConflictingIntents = false,
            Reasoning = $"Missing required context: {contextDescription}"
        };
    }

    /// <summary>
    /// Creates a combined ambiguity signal from multiple factors.
    /// </summary>
    public static AmbiguitySignals Combined(
        bool lowConfidence,
        string[] vagueVerbs,
        string? missingContext,
        double confidenceScore,
        double threshold)
    {
        var hasVagueVerbs = vagueVerbs.Length > 0;
        var hasMissingContext = !string.IsNullOrEmpty(missingContext);

        var level = DetermineLevel(lowConfidence, hasVagueVerbs, hasMissingContext);

        var reasoningParts = new List<string>();
        if (lowConfidence)
            reasoningParts.Add($"low confidence ({confidenceScore:F2} < {threshold:F2})");
        if (hasVagueVerbs)
            reasoningParts.Add($"vague verbs: {string.Join(", ", vagueVerbs)}");
        if (hasMissingContext)
            reasoningParts.Add($"missing context: {missingContext}");

        return new AmbiguitySignals
        {
            Level = level,
            HasLowConfidence = lowConfidence,
            ConfidenceScore = confidenceScore,
            HasVagueVerbs = hasVagueVerbs,
            DetectedVagueVerbs = vagueVerbs,
            HasMissingContext = hasMissingContext,
            MissingContextDescription = missingContext,
            HasConflictingIntents = false,
            Reasoning = reasoningParts.Count > 0
                ? $"Ambiguous due to: {string.Join("; ", reasoningParts)}"
                : "No ambiguity detected"
        };
    }

    private static AmbiguityLevel DetermineLevel(bool lowConfidence, bool hasVagueVerbs, bool hasMissingContext)
    {
        var signalCount = (lowConfidence ? 1 : 0) + (hasVagueVerbs ? 1 : 0) + (hasMissingContext ? 1 : 0);

        return signalCount switch
        {
            0 => AmbiguityLevel.None,
            1 => AmbiguityLevel.Low,
            2 => AmbiguityLevel.Medium,
            _ => AmbiguityLevel.High
        };
    }
}

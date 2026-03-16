namespace nirmata.Agents.Execution.Preflight;

/// <summary>
/// Analyzes user input for ambiguity signals that indicate unclear or fuzzy intent.
/// </summary>
public interface IAmbiguityAnalyzer
{
    /// <summary>
    /// Analyzes the input text and intent classification for ambiguity signals.
    /// </summary>
    /// <param name="input">The raw user input text.</param>
    /// <param name="intent">The classified intent to analyze.</param>
    /// <param name="threshold">The confidence threshold for low confidence detection.</param>
    /// <returns>Signals indicating the level and type of ambiguity detected.</returns>
    AmbiguitySignals Analyze(string input, Intent intent, double threshold = 0.9);

    /// <summary>
    /// Analyzes the input text and classification result for ambiguity signals.
    /// </summary>
    /// <param name="input">The raw user input text.</param>
    /// <param name="classification">The classification result to analyze.</param>
    /// <param name="threshold">The confidence threshold for low confidence detection.</param>
    /// <returns>Signals indicating the level and type of ambiguity detected.</returns>
    AmbiguitySignals Analyze(string input, IntentClassificationResult classification, double threshold = 0.9);
}

/// <summary>
/// Default implementation of the ambiguity analyzer that detects vague verbs, low confidence, and missing context.
/// </summary>
public sealed class AmbiguityAnalyzer : IAmbiguityAnalyzer
{
    // Vague verbs that indicate unclear intent
    private static readonly HashSet<string> VagueVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "do", "make", "fix", "change", "update", "handle", "deal", "work",
        "process", "manage", "adjust", "modify", "touch", "look", "see",
        "check", "go", "get", "put", "set", "run", "move", "take"
    };

    // Context patterns that indicate missing information
    private static readonly string[] MissingContextPatterns =
    [
        "it", "that", "this", "there", "them", "those", "these"
    ];

    /// <summary>
    /// The confidence threshold for considering an intent ambiguous.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.9;

    /// <inheritdoc />
    public AmbiguitySignals Analyze(string input, Intent intent, double threshold = 0.9)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return AmbiguitySignals.MissingContext("Empty input provided", 0.0);
        }

        var detectedVagueVerbs = DetectVagueVerbs(input);
        var hasVagueVerbs = detectedVagueVerbs.Length > 0;
        var hasLowConfidence = intent.Confidence < threshold;
        var missingContext = DetectMissingContext(input, intent);

        // If no ambiguity signals, return clear
        if (!hasLowConfidence && !hasVagueVerbs && string.IsNullOrEmpty(missingContext))
        {
            return AmbiguitySignals.Clear(intent.Confidence);
        }

        // If only one signal, return specific signal type
        if (hasLowConfidence && !hasVagueVerbs && string.IsNullOrEmpty(missingContext))
        {
            return AmbiguitySignals.LowConfidence(intent.Confidence, threshold);
        }

        if (!hasLowConfidence && hasVagueVerbs && string.IsNullOrEmpty(missingContext))
        {
            return AmbiguitySignals.VagueVerbs(detectedVagueVerbs, intent.Confidence);
        }

        if (!hasLowConfidence && !hasVagueVerbs && !string.IsNullOrEmpty(missingContext))
        {
            return AmbiguitySignals.MissingContext(missingContext, intent.Confidence);
        }

        // Multiple signals detected - return combined
        return AmbiguitySignals.Combined(
            hasLowConfidence,
            detectedVagueVerbs,
            missingContext,
            intent.Confidence,
            threshold);
    }

    /// <inheritdoc />
    public AmbiguitySignals Analyze(string input, IntentClassificationResult classification, double threshold = 0.9)
    {
        // Start with the intent analysis
        var signals = Analyze(input, classification.Intent, threshold);

        // If classification already has ambiguity signals, use those
        if (classification.Ambiguity != null)
        {
            // Merge with existing signals, keeping the most severe level
            var existing = classification.Ambiguity;

            var mergedLevel = (AmbiguityLevel)Math.Max(
                (int)signals.Level,
                (int)existing.Level);

            var mergedVagueVerbs = existing.DetectedVagueVerbs
                .Concat(signals.DetectedVagueVerbs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new AmbiguitySignals
            {
                Level = mergedLevel,
                HasLowConfidence = existing.HasLowConfidence || signals.HasLowConfidence,
                ConfidenceScore = Math.Min(existing.ConfidenceScore, signals.ConfidenceScore),
                HasVagueVerbs = existing.HasVagueVerbs || signals.HasVagueVerbs,
                DetectedVagueVerbs = mergedVagueVerbs,
                HasMissingContext = existing.HasMissingContext || signals.HasMissingContext,
                MissingContextDescription = existing.MissingContextDescription ?? signals.MissingContextDescription,
                HasConflictingIntents = existing.HasConflictingIntents || signals.HasConflictingIntents,
                Reasoning = $"Existing: {existing.Reasoning}; New: {signals.Reasoning}"
            };
        }

        return signals;
    }

    /// <summary>
    /// Detects vague verbs in the input text.
    /// </summary>
    private string[] DetectVagueVerbs(string input)
    {
        var words = input.ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r', '.', ',', '!', '?'], StringSplitOptions.RemoveEmptyEntries);

        var detected = words
            .Where(word => VagueVerbs.Contains(word.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return detected;
    }

    /// <summary>
    /// Detects missing context in the input text.
    /// </summary>
    private string? DetectMissingContext(string input, Intent intent)
    {
        var lowerInput = input.ToLowerInvariant();

        // Check for pronouns that may lack referents
        var hasUnclearPronouns = MissingContextPatterns.Any(pattern =>
        {
            // Look for pronouns that appear without clear antecedent
            var words = lowerInput.Split([' ', '\t', '\n', '\r', '.', ',', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
            return words.Contains(pattern) && words.Length < 5; // Short inputs with pronouns are often ambiguous
        });

        if (hasUnclearPronouns)
        {
            return "Unclear pronoun references detected";
        }

        // Check for write operations without clear target
        if (intent.SideEffect == SideEffect.Write)
        {
            var hasTarget = intent.Targets?.Length > 0;
            var hasSpecificFileReference = lowerInput.Contains('.') || // File extension
                                         lowerInput.Contains('/') ||   // Path separator
                                         lowerInput.Contains('\\');    // Windows path

            if (!hasTarget && !hasSpecificFileReference)
            {
                return "Write operation without clear target specification";
            }
        }

        return null;
    }
}

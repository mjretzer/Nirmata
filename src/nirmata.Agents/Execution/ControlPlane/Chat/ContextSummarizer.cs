namespace nirmata.Agents.Execution.ControlPlane.Chat;

/// <summary>
/// Summarizes and truncates chat context to fit within token budget constraints.
/// Uses a simple character-based heuristic (4 chars ≈ 1 token) for estimation.
/// </summary>
public sealed class ContextSummarizer
{
    /// <summary>
    /// Default maximum tokens for context.
    /// </summary>
    public const int DefaultMaxTokens = 2000;

    /// <summary>
    /// Characters per token for estimation (conservative: 1 token ≈ 4 chars).
    /// </summary>
    public const double CharactersPerToken = 4.0;

    private readonly int _maxTokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextSummarizer"/> class.
    /// </summary>
    /// <param name="maxTokens">Maximum token budget for context. Default is 2000.</param>
    public ContextSummarizer(int maxTokens = DefaultMaxTokens)
    {
        _maxTokens = maxTokens > 0 ? maxTokens : DefaultMaxTokens;
    }

    /// <summary>
    /// Summarizes the chat context to fit within the token budget.
    /// Priority order: State > Commands > Project > Roadmap > RecentRuns
    /// </summary>
    /// <param name="context">The context to summarize.</param>
    /// <returns>A summarized context that fits within token budget.</returns>
    public ChatContext Summarize(ChatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var maxChars = (int)(_maxTokens * CharactersPerToken);

        // Start with high-priority elements and add until budget is reached
        var state = context.State;
        var availableCommands = SummarizeCommands(context.AvailableCommands, 50); // Reserve budget for commands
        var project = SummarizeProject(context.Project, maxChars / 4);
        var roadmap = SummarizeRoadmap(context.Roadmap, maxChars / 6);
        var recentRuns = SummarizeRecentRuns(context.RecentRuns, 3);

        return new ChatContext
        {
            Project = project,
            Roadmap = roadmap,
            State = state,
            RecentRuns = recentRuns,
            AvailableCommands = availableCommands,
            IsSuccess = context.IsSuccess,
            ErrorMessage = context.ErrorMessage
        };
    }

    /// <summary>
    /// Estimates the token count for a given string.
    /// </summary>
    public int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / CharactersPerToken);
    }

    /// <summary>
    /// Truncates text to fit within specified token budget.
    /// </summary>
    public string TruncateToTokens(string? text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var maxChars = (int)(maxTokens * CharactersPerToken);
        if (text.Length <= maxChars) return text;

        return text[..(maxChars - 3)] + "...";
    }

    private static ProjectContext? SummarizeProject(ProjectContext? project, int maxDescChars)
    {
        if (project == null) return null;

        var desc = project.Description;
        if (!string.IsNullOrEmpty(desc) && desc.Length > maxDescChars)
        {
            desc = desc[..(maxDescChars - 3)] + "...";
        }

        return new ProjectContext
        {
            Name = project.Name,
            Description = desc,
            Goals = project.Goals.Take(3).ToList() // Limit goals
        };
    }

    private static RoadmapContext? SummarizeRoadmap(RoadmapContext? roadmap, int maxPhaseNameLength)
    {
        if (roadmap == null) return null;

        var phases = roadmap.Phases
            .Select(p => p.Length > maxPhaseNameLength ? p[..(maxPhaseNameLength - 3)] + "..." : p)
            .Take(5) // Limit to 5 phases
            .ToList();

        return new RoadmapContext
        {
            PhaseCount = roadmap.PhaseCount,
            Phases = phases,
            CurrentPhase = roadmap.CurrentPhase
        };
    }

    private static IReadOnlyList<CommandContext> SummarizeCommands(IReadOnlyList<CommandContext> commands, int maxDescChars)
    {
        // Take essential commands first, limit descriptions
        return commands
            .Take(8) // Limit to 8 commands
            .Select(c => new CommandContext
            {
                Name = c.Name,
                Syntax = c.Syntax,
                Description = c.Description.Length > maxDescChars
                    ? c.Description[..(maxDescChars - 3)] + "..."
                    : c.Description
            })
            .ToList();
    }

    private static IReadOnlyList<RunHistoryContext> SummarizeRecentRuns(IReadOnlyList<RunHistoryContext> runs, int maxCount)
    {
        return runs.Take(maxCount).ToList();
    }
}

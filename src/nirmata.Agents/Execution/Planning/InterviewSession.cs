namespace nirmata.Agents.Execution.Planning;

/// <summary>
/// Represents a question and answer pair in the interview.
/// </summary>
public sealed record InterviewQAPair
{
    /// <summary>
    /// The question asked by the interviewer.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// The answer provided by the user.
    /// </summary>
    public required string Answer { get; init; }

    /// <summary>
    /// The phase of the interview when this Q&A occurred.
    /// </summary>
    public required InterviewPhase Phase { get; init; }

    /// <summary>
    /// Optional timestamp of when this Q&A occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents the current state of an interview session.
/// </summary>
public enum InterviewState
{
    /// <summary>
    /// Interview has not started.
    /// </summary>
    NotStarted,

    /// <summary>
    /// In discovery phase - gathering initial requirements.
    /// </summary>
    Discovery,

    /// <summary>
    /// In clarification phase - probing unclear areas.
    /// </summary>
    Clarification,

    /// <summary>
    /// In confirmation phase - validating understanding.
    /// </summary>
    Confirmation,

    /// <summary>
    /// Interview is complete.
    /// </summary>
    Complete,

    /// <summary>
    /// Interview was cancelled or failed.
    /// </summary>
    Failed
}

/// <summary>
/// Represents the phase of the interview.
/// </summary>
public enum InterviewPhase
{
    /// <summary>
    /// Initial discovery phase.
    /// </summary>
    Discovery,

    /// <summary>
    /// Clarification phase for probing unclear areas.
    /// </summary>
    Clarification,

    /// <summary>
    /// Confirmation phase for validating understanding.
    /// </summary>
    Confirmation
}

/// <summary>
/// Tracks the state of an interview session including Q&A history and draft project spec.
/// </summary>
public sealed class InterviewSession
{
    /// <summary>
    /// Unique identifier for this interview session.
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The current state of the interview.
    /// </summary>
    public InterviewState State { get; set; } = InterviewState.NotStarted;

    /// <summary>
    /// The current phase of the interview.
    /// </summary>
    public InterviewPhase CurrentPhase { get; set; } = InterviewPhase.Discovery;

    /// <summary>
    /// The collection of Q&A pairs in this session.
    /// </summary>
    public List<InterviewQAPair> QAPairs { get; init; } = new();

    /// <summary>
    /// The current draft of the project specification being built.
    /// </summary>
    public ProjectSpecDraft? ProjectDraft { get; set; }

    /// <summary>
    /// Any additional context data collected during the interview.
    /// </summary>
    public Dictionary<string, object> ContextData { get; init; } = new();

    /// <summary>
    /// When the interview session started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the interview session completed (if applicable).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// The run ID associated with this interview session.
    /// </summary>
    public string? RunId { get; set; }
}

/// <summary>
/// Represents the draft project specification being built during the interview.
/// </summary>
public sealed class ProjectSpecDraft
{
    /// <summary>
    /// The project name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The project description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The primary programming language or technology stack.
    /// </summary>
    public string? TechnologyStack { get; set; }

    /// <summary>
    /// The project goals or objectives.
    /// </summary>
    public List<string> Goals { get; init; } = new();

    /// <summary>
    /// The target audience or users.
    /// </summary>
    public string? TargetAudience { get; set; }

    /// <summary>
    /// Key features or requirements identified.
    /// </summary>
    public List<string> KeyFeatures { get; init; } = new();

    /// <summary>
    /// Constraints or limitations.
    /// </summary>
    public List<string> Constraints { get; init; } = new();

    /// <summary>
    /// Assumptions made during the interview.
    /// </summary>
    public List<string> Assumptions { get; init; } = new();

    /// <summary>
    /// Additional metadata collected.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

namespace nirmata.Agents.Execution.ControlPlane.Chat;

/// <summary>
/// Interface for assembling workspace context for chat responses.
/// </summary>
public interface IChatContextAssembly
{
    /// <summary>
    /// Assembles workspace context including specs, state, and available commands.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation with the assembled context.</returns>
    Task<ChatContext> AssembleAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the assembled workspace context for a chat prompt.
/// </summary>
public sealed class ChatContext
{
    /// <summary>
    /// Project information if available.
    /// </summary>
    public ProjectContext? Project { get; init; }

    /// <summary>
    /// Roadmap information if available.
    /// </summary>
    public RoadmapContext? Roadmap { get; init; }

    /// <summary>
    /// Current workspace state.
    /// </summary>
    public StateContext State { get; init; } = new();

    /// <summary>
    /// Recent run history.
    /// </summary>
    public IReadOnlyList<RunHistoryContext> RecentRuns { get; init; } = Array.Empty<RunHistoryContext>();

    /// <summary>
    /// Available commands with descriptions.
    /// </summary>
    public IReadOnlyList<CommandContext> AvailableCommands { get; init; } = Array.Empty<CommandContext>();

    /// <summary>
    /// Whether context assembly was successful.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Error message if context assembly failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Project context information.
/// </summary>
public sealed class ProjectContext
{
    /// <summary>
    /// Project name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Project description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Project goals or objectives.
    /// </summary>
    public IReadOnlyList<string> Goals { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Roadmap context information.
/// </summary>
public sealed class RoadmapContext
{
    /// <summary>
    /// Number of phases in the roadmap.
    /// </summary>
    public int PhaseCount { get; init; }

    /// <summary>
    /// List of phase names/summaries.
    /// </summary>
    public IReadOnlyList<string> Phases { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Current phase if available.
    /// </summary>
    public string? CurrentPhase { get; init; }
}

/// <summary>
/// Workspace state context information.
/// </summary>
public sealed class StateContext
{
    /// <summary>
    /// Current cursor position (phase/task).
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Current phase ID.
    /// </summary>
    public string? CurrentPhaseId { get; init; }

    /// <summary>
    /// Current task ID.
    /// </summary>
    public string? CurrentTaskId { get; init; }

    /// <summary>
    /// Last run status.
    /// </summary>
    public string? LastRunStatus { get; init; }
}

/// <summary>
/// Recent run history context.
/// </summary>
public sealed class RunHistoryContext
{
    /// <summary>
    /// Run ID.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Run status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Run phase.
    /// </summary>
    public string? Phase { get; init; }

    /// <summary>
    /// Run timestamp.
    /// </summary>
    public DateTime? Timestamp { get; init; }
}

/// <summary>
/// Available command context.
/// </summary>
public sealed class CommandContext
{
    /// <summary>
    /// Command name (e.g., "run", "plan").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Command syntax (e.g., "/run --task-id <id>").
    /// </summary>
    public required string Syntax { get; init; }

    /// <summary>
    /// Command description.
    /// </summary>
    public required string Description { get; init; }
}

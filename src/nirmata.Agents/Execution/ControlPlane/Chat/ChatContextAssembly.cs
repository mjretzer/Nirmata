using System.Text.Json;
using nirmata.Agents.Execution.Preflight;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Engine.Spec;
using nirmata.Aos.Engine.Stores;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.ControlPlane.Chat;

/// <summary>
/// Assembles workspace context for chat responses, including project, roadmap, state, and commands.
/// </summary>
public sealed class ChatContextAssembly : IChatContextAssembly
{
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private readonly ICommandRegistry _commandRegistry;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(5);
    private ChatContext? _cachedContext;
    private DateTime _cacheTimestamp;

    /// <summary>
    /// Maximum tokens for context assembly (approximate, based on character count heuristic).
    /// </summary>
    public int MaxContextTokens { get; init; } = 2000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatContextAssembly"/> class.
    /// </summary>
    public ChatContextAssembly(
        IWorkspace workspace,
        IStateStore stateStore,
        ICommandRegistry commandRegistry)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
    }

    /// <inheritdoc />
    public Task<ChatContext> AssembleAsync(CancellationToken cancellationToken = default)
    {
        // Check cache
        if (_cachedContext != null && DateTime.UtcNow - _cacheTimestamp < _cacheTtl)
        {
            return Task.FromResult(_cachedContext);
        }

        try
        {
            var context = BuildContext();
            _cachedContext = context;
            _cacheTimestamp = DateTime.UtcNow;
            return Task.FromResult(context);
        }
        catch (Exception ex)
        {
            // Return degraded context on failure
            return Task.FromResult(new ChatContext
            {
                IsSuccess = false,
                ErrorMessage = $"Failed to assemble context: {ex.Message}",
                State = new StateContext(),
                AvailableCommands = GetAvailableCommands()
            });
        }
    }

    private ChatContext BuildContext()
    {
        var specStore = SpecStore.FromWorkspace(_workspace);
        var snapshot = _stateStore.ReadSnapshot();

        // Build project context
        ProjectContext? project = null;
        try
        {
            var projectDoc = specStore.Inner.ReadProject();
            if (projectDoc?.Project != null)
            {
                project = new ProjectContext
                {
                    Name = projectDoc.Project.Name,
                    Description = Truncate(projectDoc.Project.Description, 500),
                    Goals = Array.Empty<string>() // Future: extract goals if available
                };
            }
        }
        catch
        {
            // Project not defined - that's ok
        }

        // Build roadmap context
        RoadmapContext? roadmap = null;
        try
        {
            var roadmapDoc = specStore.Inner.ReadRoadmap();
            if (roadmapDoc?.Roadmap?.Items != null)
            {
                var phases = roadmapDoc.Roadmap.Items
                    .Where(i => i.Kind.Equals("phase", StringComparison.OrdinalIgnoreCase))
                    .Select(i => i.Title)
                    .ToList();

                roadmap = new RoadmapContext
                {
                    PhaseCount = phases.Count,
                    Phases = phases.Take(10).ToList(), // Limit to avoid token bloat
                    CurrentPhase = snapshot?.Cursor?.PhaseId
                };
            }
        }
        catch
        {
            // Roadmap not defined - that's ok
        }

        // Build state context
        var state = new StateContext
        {
            CurrentPhaseId = snapshot?.Cursor?.PhaseId,
            CurrentTaskId = snapshot?.Cursor?.TaskId,
            LastRunStatus = snapshot?.Cursor?.TaskStatus,
            Cursor = BuildCursorDisplay(snapshot?.Cursor)
        };

        // Build recent runs (from events)
        var recentRuns = GetRecentRuns();

        // Build available commands
        var commands = GetAvailableCommands();

        return new ChatContext
        {
            Project = project,
            Roadmap = roadmap,
            State = state,
            RecentRuns = recentRuns,
            AvailableCommands = commands,
            IsSuccess = true
        };
    }

    private IReadOnlyList<RunHistoryContext> GetRecentRuns()
    {
        try
        {
            var response = _stateStore.TailEvents(new StateEventTailRequest { MaxItems = 5 });
            if (response?.Items == null) return Array.Empty<RunHistoryContext>();

            return response.Items
                .Where(e => GetEventType(e).Contains("run", StringComparison.OrdinalIgnoreCase))
                .Select(e => new RunHistoryContext
                {
                    RunId = GetEventId(e) ?? "unknown",
                    Status = GetEventType(e) ?? "unknown",
                    Timestamp = GetEventTimestamp(e)
                })
                .ToList();
        }
        catch
        {
            return Array.Empty<RunHistoryContext>();
        }
    }

    private IReadOnlyList<CommandContext> GetAvailableCommands()
    {
        var commands = _commandRegistry.GetAllCommands();
        return commands.Select(c => new CommandContext
        {
            Name = c.Name,
            Syntax = $"/{c.Name}",
            Description = c.Description ?? "No description available"
        }).ToList();
    }

    private static string? BuildCursorDisplay(nirmata.Aos.Contracts.State.StateCursor? cursor)
    {
        if (cursor == null) return null;
        if (!string.IsNullOrWhiteSpace(cursor.TaskId))
            return cursor.TaskId;
        if (!string.IsNullOrWhiteSpace(cursor.PhaseId))
            return cursor.PhaseId;
        return null;
    }

    private static string GetEventType(StateEventEntry entry)
    {
        if (entry.Payload.TryGetProperty("eventType", out var prop))
            return prop.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static string? GetEventId(StateEventEntry entry)
    {
        if (entry.Payload.TryGetProperty("eventId", out var prop))
            return prop.GetString();
        if (entry.Payload.TryGetProperty("id", out var idProp))
            return idProp.GetString();
        return null;
    }

    private static DateTime? GetEventTimestamp(StateEventEntry entry)
    {
        if (entry.Payload.TryGetProperty("timestamp", out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            if (prop.TryGetDateTime(out var dt))
                return dt;
        }
        return null;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}

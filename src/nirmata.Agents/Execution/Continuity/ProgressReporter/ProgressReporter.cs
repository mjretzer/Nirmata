using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Continuity.ProgressReporter;

/// <summary>
/// Generates deterministic progress reports from current execution state.
/// Reads from IStateStore and produces structured progress output with blockers and recommendations.
/// </summary>
public sealed class ProgressReporter : IProgressReporter
{
    private readonly IStateStore _stateStore;
    private readonly IRunManager? _runManager;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressReporter"/> class.
    /// </summary>
    public ProgressReporter(IStateStore stateStore, IRunManager? runManager = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _runManager = runManager;
    }

    /// <inheritdoc />
    public Task<ProgressReport> ReportAsync(string format = "json", CancellationToken ct = default)
    {
        var snapshot = _stateStore.ReadSnapshot();
        var report = BuildProgressReport(snapshot);
        return Task.FromResult(report);
    }

    /// <inheritdoc />
    public Task<string> ReportAsStringAsync(string format = "json", CancellationToken ct = default)
    {
        var report = ReportAsync(format, ct).Result;

        return format.ToLowerInvariant() switch
        {
            "json" => Task.FromResult(SerializeToJson(report)),
            "markdown" => Task.FromResult(FormatAsMarkdown(report)),
            _ => throw new ArgumentException($"Unsupported format '{format}'. Supported formats: json, markdown.", nameof(format))
        };
    }

    private ProgressReport BuildProgressReport(StateSnapshot snapshot)
    {
        var cursor = snapshot?.Cursor ?? new StateCursor();
        var blockers = DetectBlockers(cursor);
        var nextCommand = RecommendNextCommand(cursor, blockers);
        var hasActiveExecution = HasActiveExecution(cursor);
        var runId = hasActiveExecution ? InferRunId(cursor) : null;
        var runStatus = GetRunStatus(runId);

        return new ProgressReport
        {
            Cursor = MapToProgressCursor(cursor),
            Blockers = blockers,
            NextCommand = nextCommand,
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            RunId = runId,
            HasActiveExecution = hasActiveExecution,
            RunStatus = runStatus
        };
    }

    private ProgressCursor MapToProgressCursor(StateCursor cursor)
    {
        return new ProgressCursor
        {
            Phase = cursor.PhaseId,
            Milestone = cursor.MilestoneId,
            Task = cursor.TaskId,
            Step = cursor.StepId
        };
    }

    private IReadOnlyList<Blocker> DetectBlockers(StateCursor cursor)
    {
        var blockers = new List<Blocker>();

        // Check for explicit blocked status at any level
        if (IsBlockedStatus(cursor.StepStatus))
        {
            blockers.Add(new Blocker
            {
                Task = cursor.TaskId,
                Type = "step-blocked",
                Severity = "high",
                Description = $"Step '{cursor.StepId}' is blocked."
            });
        }

        if (IsBlockedStatus(cursor.TaskStatus))
        {
            blockers.Add(new Blocker
            {
                Task = cursor.TaskId,
                Type = "task-blocked",
                Severity = "high",
                Description = $"Task '{cursor.TaskId}' is blocked."
            });
        }

        if (IsBlockedStatus(cursor.PhaseStatus))
        {
            blockers.Add(new Blocker
            {
                Task = cursor.TaskId,
                Type = "phase-blocked",
                Severity = "medium",
                Description = $"Phase '{cursor.PhaseId}' is blocked."
            });
        }

        if (IsBlockedStatus(cursor.MilestoneStatus))
        {
            blockers.Add(new Blocker
            {
                Task = cursor.TaskId,
                Type = "milestone-blocked",
                Severity = "medium",
                Description = $"Milestone '{cursor.MilestoneId}' is blocked."
            });
        }

        // Check for error states
        if (IsErrorStatus(cursor.StepStatus))
        {
            blockers.Add(new Blocker
            {
                Task = cursor.TaskId,
                Type = "step-error",
                Severity = "critical",
                Description = $"Step '{cursor.StepId}' encountered an error."
            });
        }

        if (IsErrorStatus(cursor.TaskStatus))
        {
            blockers.Add(new Blocker
            {
                Task = cursor.TaskId,
                Type = "task-error",
                Severity = "critical",
                Description = $"Task '{cursor.TaskId}' encountered an error."
            });
        }

        // Check for missing dependencies (no active task but phase/milestone in progress)
        if (string.IsNullOrEmpty(cursor.TaskId) &&
            (IsInProgressStatus(cursor.PhaseStatus) || IsInProgressStatus(cursor.MilestoneStatus)))
        {
            blockers.Add(new Blocker
            {
                Type = "dependency-missing",
                Severity = "low",
                Description = "No active task but phase/milestone is in progress. Task selection may be needed."
            });
        }

        return blockers;
    }

    private static bool IsBlockedStatus(string? status) =>
        string.Equals(status, StateCursorStatuses.Blocked, StringComparison.OrdinalIgnoreCase);

    private static bool IsErrorStatus(string? status) =>
        !string.IsNullOrEmpty(status) &&
        (status.Contains("error", StringComparison.OrdinalIgnoreCase) ||
         status.Contains("failed", StringComparison.OrdinalIgnoreCase));

    private static bool IsInProgressStatus(string? status) =>
        string.Equals(status, StateCursorStatuses.InProgress, StringComparison.OrdinalIgnoreCase);

    private static bool IsNotStartedStatus(string? status) =>
        string.Equals(status, StateCursorStatuses.NotStarted, StringComparison.OrdinalIgnoreCase);

    private NextCommand RecommendNextCommand(StateCursor cursor, IReadOnlyList<Blocker> blockers)
    {
        // If there are critical blockers, recommend fix workflow
        if (blockers.Any(b => b.Severity == "critical"))
        {
            return new NextCommand
            {
                Command = "fix",
                Args = new Dictionary<string, object?>
                {
                    ["target"] = blockers.First(b => b.Severity == "critical").Task ?? "current"
                },
                Reason = "Critical errors detected. Initiating fix workflow to resolve issues."
            };
        }

        // If there are high severity blockers, recommend resume with attention
        if (blockers.Any(b => b.Severity == "high"))
        {
            return new NextCommand
            {
                Command = "resume",
                Args = new Dictionary<string, object?>
                {
                    ["focus"] = blockers.First(b => b.Severity == "high").Task ?? "blocked"
                },
                Reason = "Blocked items detected. Resume execution with focus on unblocking."
            };
        }

        // If there are medium severity blockers, recommend resume to address them
        if (blockers.Any(b => b.Severity == "medium"))
        {
            return new NextCommand
            {
                Command = "resume",
                Reason = "Blocked phase or milestone detected. Resume to unblock the current execution."
            };
        }

        // If no active execution, recommend start
        if (string.IsNullOrEmpty(cursor.TaskId) &&
            string.IsNullOrEmpty(cursor.PhaseId) &&
            string.IsNullOrEmpty(cursor.MilestoneId))
        {
            return new NextCommand
            {
                Command = "start",
                Reason = "No active execution found. Start a new workflow run."
            };
        }

        // If task is in progress but no step, recommend plan
        if (!string.IsNullOrEmpty(cursor.TaskId) &&
            IsInProgressStatus(cursor.TaskStatus) &&
            string.IsNullOrEmpty(cursor.StepId))
        {
            return new NextCommand
            {
                Command = "plan",
                Args = new Dictionary<string, object?>
                {
                    ["task"] = cursor.TaskId
                },
                Reason = "Task is active but no step is selected. Generate a plan for task execution."
            };
        }

        // If step is not started, recommend execute
        if (!string.IsNullOrEmpty(cursor.StepId) &&
            IsNotStartedStatus(cursor.StepStatus))
        {
            return new NextCommand
            {
                Command = "execute",
                Args = new Dictionary<string, object?>
                {
                    ["step"] = cursor.StepId,
                    ["task"] = cursor.TaskId
                },
                Reason = "Step is ready to execute."
            };
        }

        // If step is in progress, recommend continue
        if (!string.IsNullOrEmpty(cursor.StepId) &&
            IsInProgressStatus(cursor.StepStatus))
        {
            return new NextCommand
            {
                Command = "continue",
                Reason = "Step is in progress. Continue execution."
            };
        }

        // Default: recommend continue
        return new NextCommand
        {
            Command = "continue",
            Reason = "Continue with current execution flow."
        };
    }

    private static bool HasActiveExecution(StateCursor cursor)
    {
        return !string.IsNullOrEmpty(cursor.TaskId) ||
               !string.IsNullOrEmpty(cursor.PhaseId) ||
               !string.IsNullOrEmpty(cursor.MilestoneId);
    }

    private string? InferRunId(StateCursor cursor)
    {
        // Derive the active run ID from canonical events.ndjson.
        // An active run is the most recent run.started event with no matching run.completed or run.failed.
        var allEvents = _stateStore.TailEvents(new StateEventTailRequest { SinceLine = 0 });
        var startedRunIds = new List<string>();
        var closedRunIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in allEvents.Items)
        {
            var payload = entry.Payload;
            if (!payload.TryGetProperty("eventType", out var evtTypeProp) ||
                evtTypeProp.ValueKind != JsonValueKind.String)
                continue;

            if (!payload.TryGetProperty("runId", out var runIdProp) ||
                runIdProp.ValueKind != JsonValueKind.String)
                continue;

            var eventType = evtTypeProp.GetString()!;
            var runId = runIdProp.GetString()!;

            if (string.Equals(eventType, "run.started", StringComparison.Ordinal))
                startedRunIds.Add(runId);
            else if (string.Equals(eventType, "run.completed", StringComparison.Ordinal) ||
                     string.Equals(eventType, "run.failed", StringComparison.Ordinal))
                closedRunIds.Add(runId);
        }

        return startedRunIds.LastOrDefault(id => !closedRunIds.Contains(id));
    }

    private string? GetRunStatus(string? runId)
    {
        if (string.IsNullOrEmpty(runId) || _runManager is null)
        {
            return null;
        }

        try
        {
            var runInfo = _runManager.GetRun(runId);
            return runInfo?.Status;
        }
        catch
        {
            return null;
        }
    }

    private static string SerializeToJson(ProgressReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static string FormatAsMarkdown(ProgressReport report)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# Progress Report");
        sb.AppendLine();
        sb.AppendLine($"**Timestamp:** {report.Timestamp}");
        sb.AppendLine($"**Active Execution:** {(report.HasActiveExecution ? "Yes" : "No")}");
        if (!string.IsNullOrEmpty(report.RunId))
        {
            sb.AppendLine($"**Run ID:** {report.RunId}");
        }
        if (!string.IsNullOrEmpty(report.RunStatus))
        {
            sb.AppendLine($"**Run Status:** {report.RunStatus}");
        }
        sb.AppendLine();

        sb.AppendLine("## Cursor Position");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(report.Cursor.Milestone))
        {
            sb.AppendLine($"- **Milestone:** {report.Cursor.Milestone}");
        }
        if (!string.IsNullOrEmpty(report.Cursor.Phase))
        {
            sb.AppendLine($"- **Phase:** {report.Cursor.Phase}");
        }
        if (!string.IsNullOrEmpty(report.Cursor.Task))
        {
            sb.AppendLine($"- **Task:** {report.Cursor.Task}");
        }
        if (!string.IsNullOrEmpty(report.Cursor.Step))
        {
            sb.AppendLine($"- **Step:** {report.Cursor.Step}");
        }
        if (string.IsNullOrEmpty(report.Cursor.Milestone) &&
            string.IsNullOrEmpty(report.Cursor.Phase) &&
            string.IsNullOrEmpty(report.Cursor.Task) &&
            string.IsNullOrEmpty(report.Cursor.Step))
        {
            sb.AppendLine("*No active cursor position*");
        }
        sb.AppendLine();

        sb.AppendLine("## Blockers");
        sb.AppendLine();
        if (report.Blockers.Count == 0)
        {
            sb.AppendLine("*No blockers detected*");
        }
        else
        {
            foreach (var blocker in report.Blockers)
            {
                sb.AppendLine($"### {blocker.Type} ({blocker.Severity})");
                if (!string.IsNullOrEmpty(blocker.Task))
                {
                    sb.AppendLine($"**Task:** {blocker.Task}");
                }
                sb.AppendLine($"**Description:** {blocker.Description}");
                sb.AppendLine();
            }
        }
        sb.AppendLine();

        sb.AppendLine("## Next Recommended Command");
        sb.AppendLine();
        sb.AppendLine($"**Command:** `{report.NextCommand.Command}`");
        if (report.NextCommand.Args?.Count > 0)
        {
            sb.AppendLine("**Arguments:**");
            foreach (var arg in report.NextCommand.Args)
            {
                sb.AppendLine($"- `{arg.Key}`: {arg.Value}");
            }
        }
        if (!string.IsNullOrEmpty(report.NextCommand.Reason))
        {
            sb.AppendLine($"**Reason:** {report.NextCommand.Reason}");
        }

        return sb.ToString();
    }
}

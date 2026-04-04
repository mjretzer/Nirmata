using System.Text.Json;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Persistence.State;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Continuity;

/// <summary>
/// Manages pause and resume operations for workflow execution.
/// Captures handoff snapshots and reconstructs execution state for deterministic continuation.
/// </summary>
public sealed class PauseResumeManager : IPauseResumeManager
{
    private readonly IStateStore _stateStore;
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly IRunRepository _runRepository;
    private readonly IWorkspace _workspace;
    private readonly IHandoffStateStore _handoffStateStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PauseResumeManager"/> class.
    /// </summary>
    public PauseResumeManager(
        IStateStore stateStore,
        IRunLifecycleManager runLifecycleManager,
        IRunRepository runRepository,
        IWorkspace workspace,
        IHandoffStateStore handoffStateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _runRepository = runRepository ?? throw new ArgumentNullException(nameof(runRepository));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _handoffStateStore = handoffStateStore ?? throw new ArgumentNullException(nameof(handoffStateStore));
    }

    /// <inheritdoc />
    public async Task<HandoffMetadata> PauseAsync(string? reason = null, CancellationToken ct = default)
    {
        // Read current state to get cursor position
        var snapshot = _stateStore.ReadSnapshot();
        if (snapshot?.Cursor?.TaskId is null)
        {
            throw new InvalidOperationException("No active execution found. Cannot pause without an active task.");
        }

        var taskId = snapshot.Cursor.TaskId;
        var runId = await GetCurrentRunIdFromEventsAsync();

        if (string.IsNullOrEmpty(runId))
        {
            throw new InvalidOperationException("No active run found. Cannot pause without an active run.");
        }

        // Build handoff state
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var handoffState = new HandoffState
        {
            SchemaVersion = "1.0",
            Timestamp = timestamp,
            SourceRunId = runId,
            Cursor = snapshot.Cursor,
            TaskContext = await BuildTaskContextAsync(taskId, runId, ct),
            Scope = await ExtractScopeConstraintsAsync(ct),
            NextCommand = await GetNextCommandAsync(snapshot, ct),
            Reason = reason
        };

        // Write handoff to disk
        _handoffStateStore.WriteHandoff(handoffState);

        // Record pause in run lifecycle
        await _runLifecycleManager.RecordCommandAsync(runId, "continuity", "pause", "completed", ct);

        return new HandoffMetadata
        {
            Timestamp = timestamp,
            SourceRunId = runId,
            HandoffPath = _handoffStateStore.HandoffPath,
            Reason = reason
        };
    }

    /// <inheritdoc />
    public async Task<ResumeResult> ResumeAsync(CancellationToken ct = default)
    {
        // Validate and read handoff
        var validationResult = await ValidateHandoffAsync(ct);
        if (!validationResult.IsValid)
        {
            throw new InvalidDataException(
                $"Handoff validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        var handoff = validationResult.Handoff!;

        // Start new continuation run
        var newRunContext = await _runLifecycleManager.StartRunAsync(ct);

        // Update handoff with continuation link
        var updatedHandoff = handoff with
        {
            ContinuationRunId = newRunContext.RunId
        };
        _handoffStateStore.WriteHandoff(updatedHandoff);

        // Restore cursor into canonical state by emitting a cursor.set event to events.ndjson.
        // This ensures state.json is derived from on-disk artifacts, not from in-memory assumptions.
        EmitCursorSetEvent(handoff.Cursor);

        // Record resume in new run
        await _runLifecycleManager.RecordCommandAsync(
            newRunContext.RunId,
            "continuity",
            "resume",
            "completed",
            ct);

        return new ResumeResult
        {
            RunId = newRunContext.RunId,
            SourceRunId = handoff.SourceRunId,
            Status = ResumeStatus.Success,
            Cursor = handoff.Cursor
        };
    }

    /// <inheritdoc />
    public async Task<ResumeResult> ResumeFromRunAsync(string runId, CancellationToken ct = default)
    {
        // Validate run exists
        if (!await _runRepository.ExistsAsync(runId, ct))
        {
            throw new DirectoryNotFoundException($"Run '{runId}' not found in evidence store.");
        }

        // Get run details
        var runResponse = await _runRepository.GetAsync(runId, ct);
        if (runResponse is null)
        {
            throw new InvalidDataException($"Run '{runId}' data is corrupted or incomplete.");
        }

        // Read evidence folder artifacts
        var evidencePath = GetEvidenceFolderPath(runId);
        if (!Directory.Exists(evidencePath))
        {
            throw new DirectoryNotFoundException($"Evidence folder for run '{runId}' not found at '{evidencePath}'.");
        }

        // Try to read summary.json for run context
        var summaryPath = Path.Combine(evidencePath, "summary.json");
        var packetPath = Path.Combine(evidencePath, "artifacts", "packet.json");

        StateCursor? restoredCursor = null;
        TaskContext? taskContext = null;

        if (File.Exists(summaryPath))
        {
            try
            {
                var summaryJson = await File.ReadAllTextAsync(summaryPath, ct);
                using var summaryDoc = JsonDocument.Parse(summaryJson);

                // Extract cursor information if available
                if (summaryDoc.RootElement.TryGetProperty("cursor", out var cursorElement))
                {
                    restoredCursor = JsonSerializer.Deserialize<StateCursor>(cursorElement.GetRawText(), JsonOptions);
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Failed to parse summary.json for run '{runId}'.", ex);
            }
        }

        // Read packet.json for task context
        if (File.Exists(packetPath))
        {
            try
            {
                var packetJson = await File.ReadAllTextAsync(packetPath, ct);
                using var packetDoc = JsonDocument.Parse(packetJson);

                if (packetDoc.RootElement.TryGetProperty("taskId", out var taskIdElement))
                {
                    var taskId = taskIdElement.GetString() ?? "unknown";
                    taskContext = new TaskContext
                    {
                        TaskId = taskId,
                        Status = "resumed",
                        ExecutionPacket = new ExecutionPacketRef
                        {
                            RunId = runId,
                            Path = $"artifacts/packet.json",
                            Hash = ComputeHash(packetJson)
                        }
                    };
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Failed to parse packet.json for run '{runId}'.", ex);
            }
        }

        // Build handoff state from historical run
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var handoffState = new HandoffState
        {
            SchemaVersion = "1.0",
            Timestamp = timestamp,
            SourceRunId = runId,
            Cursor = restoredCursor ?? new StateCursor(),
            TaskContext = taskContext ?? new TaskContext
            {
                TaskId = "unknown",
                Status = "resumed-from-history"
            },
            Scope = await ExtractScopeConstraintsAsync(ct),
            NextCommand = new NextCommand { Name = "continue" }
        };

        // Write handoff state
        _handoffStateStore.WriteHandoff(handoffState);

        // Restore cursor into canonical state by emitting a cursor.set event to events.ndjson.
        // This ensures the post-resume state.json reflects the cursor derived from historical evidence.
        EmitCursorSetEvent(handoffState.Cursor);

        // Start new continuation run
        var newRunContext = await _runLifecycleManager.StartRunAsync(ct);

        // Update handoff with continuation link
        var updatedHandoff = handoffState with
        {
            ContinuationRunId = newRunContext.RunId
        };
        _handoffStateStore.WriteHandoff(updatedHandoff);

        // Record the resume-from-run event
        await _runLifecycleManager.RecordCommandAsync(
            newRunContext.RunId,
            "continuity",
            "resume-from-run",
            "completed",
            ct);

        return new ResumeResult
        {
            RunId = newRunContext.RunId,
            SourceRunId = runId,
            Status = ResumeStatus.Success,
            Cursor = handoffState.Cursor
        };
    }

    /// <inheritdoc />
    public Task<HandoffValidationResult> ValidateHandoffAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_handoffStateStore.Exists())
            {
                return Task.FromResult(new HandoffValidationResult
                {
                    IsValid = false,
                    Errors = new[] { "No handoff.json file exists." }
                });
            }

            var handoff = _handoffStateStore.ReadHandoff();
            var errors = new List<string>();

            // Validate schema version
            if (string.IsNullOrEmpty(handoff.SchemaVersion))
            {
                errors.Add("Missing schemaVersion field.");
            }
            else if (handoff.SchemaVersion != "1.0")
            {
                errors.Add($"Unsupported schemaVersion '{handoff.SchemaVersion}'. Expected '1.0'.");
            }

            // Validate required fields
            if (string.IsNullOrEmpty(handoff.Timestamp))
            {
                errors.Add("Missing timestamp field.");
            }

            if (string.IsNullOrEmpty(handoff.SourceRunId))
            {
                errors.Add("Missing sourceRunId field.");
            }

            if (handoff.Cursor is null)
            {
                errors.Add("Missing cursor field.");
            }

            if (handoff.TaskContext is null)
            {
                errors.Add("Missing taskContext field.");
            }

            if (handoff.Scope is null)
            {
                errors.Add("Missing scope field.");
            }

            if (handoff.NextCommand is null)
            {
                errors.Add("Missing nextCommand field.");
            }

            return Task.FromResult(new HandoffValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Handoff = errors.Count == 0 ? handoff : null
            });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new HandoffValidationResult
            {
                IsValid = false,
                Errors = new[] { $"Invalid JSON in handoff file: {ex.Message}" }
            });
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult(new HandoffValidationResult
            {
                IsValid = false,
                Errors = new[] { "handoff.json file not found." }
            });
        }
    }

    /// <summary>
    /// Derives the active run ID from canonical <c>events.ndjson</c> rather than from repository heuristics.
    /// An active run is the most recent <c>run.started</c> event with no matching <c>run.completed</c> or <c>run.failed</c>.
    /// </summary>
    private Task<string?> GetCurrentRunIdFromEventsAsync()
    {
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

        var activeRunId = startedRunIds.LastOrDefault(id => !closedRunIds.Contains(id));
        return Task.FromResult(activeRunId);
    }

    /// <summary>
    /// Emits a <c>cursor.set</c> event to <c>.aos/state/events.ndjson</c> so the canonical
    /// state snapshot reflects the cursor being restored.
    /// </summary>
    private void EmitCursorSetEvent(StateCursor cursor)
    {
        using var doc = JsonSerializer.SerializeToDocument(new
        {
            eventType = "cursor.set",
            cursor = new
            {
                milestoneId = cursor.MilestoneId,
                phaseId = cursor.PhaseId,
                taskId = cursor.TaskId,
                stepId = cursor.StepId,
                milestoneStatus = cursor.MilestoneStatus,
                phaseStatus = cursor.PhaseStatus,
                taskStatus = cursor.TaskStatus,
                stepStatus = cursor.StepStatus
            },
            timestamp = DateTimeOffset.UtcNow.ToString("O")
        }, DeterministicJsonOptions.Standard);
        _stateStore.AppendEvent(doc.RootElement);
    }

    private async Task<TaskContext> BuildTaskContextAsync(string taskId, string runId, CancellationToken ct)
    {
        // Read partial results if available from evidence folder
        var partialResults = new Dictionary<string, object>();
        var evidencePath = GetEvidenceFolderPath(runId);
        var resultsPath = Path.Combine(evidencePath, "artifacts", "partial-results.json");

        if (File.Exists(resultsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(resultsPath, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    partialResults[prop.Name] = prop.Value.ToString() ?? "";
                }
            }
            catch
            {
                // Ignore partial results errors
            }
        }

        return new TaskContext
        {
            TaskId = taskId,
            Status = "paused",
            PartialResults = partialResults.Count > 0 ? partialResults : null,
            ExecutionPacket = new ExecutionPacketRef
            {
                RunId = runId,
                Path = "artifacts/packet.json"
            }
        };
    }

    private async Task<ScopeConstraints> ExtractScopeConstraintsAsync(CancellationToken ct)
    {
        // Extract scope from workspace configuration or state
        // For now, return empty scope (to be enhanced with actual scope detection)
        return new ScopeConstraints();
    }

    private async Task<NextCommand> GetNextCommandAsync(StateSnapshot snapshot, CancellationToken ct)
    {
        // Determine next command based on current state
        // This is a simplified implementation - real logic would analyze state transitions
        return new NextCommand
        {
            Name = "continue",
            Group = "orchestrator"
        };
    }

    private string GetEvidenceFolderPath(string runId)
    {
        return Path.Combine(_workspace.AosRootPath, "evidence", "runs", runId);
    }

    private static string? ComputeHash(string content)
    {
        // Simplified hash computation - in production use proper hashing
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content[..Math.Min(100, content.Length)]));
    }
}

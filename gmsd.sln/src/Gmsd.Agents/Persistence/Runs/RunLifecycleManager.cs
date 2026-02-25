using System.Text.Json;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Models.Results;
using Gmsd.Agents.Persistence.State;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Persistence.Runs;

/// <summary>
/// Run lifecycle manager implementation that wraps Engine stores and manages evidence folders.
/// </summary>
public sealed class RunLifecycleManager : IRunLifecycleManager
{
    private readonly IRunRepository _runRepository;
    private readonly IWorkspace _workspace;
    private readonly IDeterministicJsonSerializer _jsonSerializer;
    private readonly IEventStore _eventStore;
    private readonly List<CommandRecord> _commandRecords = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RunLifecycleManager" /> class.
    /// </summary>
    public RunLifecycleManager(
        IRunRepository runRepository,
        IWorkspace workspace,
        IDeterministicJsonSerializer jsonSerializer,
        IEventStore eventStore)
    {
        _runRepository = runRepository ?? throw new ArgumentNullException(nameof(runRepository));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <inheritdoc />
    public async Task<RunContext> StartRunAsync(CancellationToken ct = default)
    {
        var runId = GenerateRunId();
        var correlationId = Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;

        var runContext = new RunContext
        {
            RunId = runId,
            CorrelationId = correlationId,
            WorkflowName = "orchestrator",
            StartedAt = startedAt,
            CurrentStep = "started"
        };

        // Create evidence folder structure
        var evidenceFolder = GetEvidenceFolderPath(runId);
        Directory.CreateDirectory(evidenceFolder);
        Directory.CreateDirectory(Path.Combine(evidenceFolder, "logs"));
        Directory.CreateDirectory(Path.Combine(evidenceFolder, "artifacts"));

        // Write run.json metadata using deterministic JSON
        var runMetadata = new
        {
            schemaVersion = 1,
            runId,
            correlationId,
            status = "open",
            startedAt = runContext.StartedAt,
            workflowName = runContext.WorkflowName
        };

        var runJsonPath = Path.Combine(evidenceFolder, "run.json");
        _jsonSerializer.WriteAtomic(runJsonPath, runMetadata, DeterministicJsonOptions.Standard, writeIndented: true);

        // Append run started event to events.ndjson using EventStore
        using var startedEventDoc = JsonSerializer.SerializeToDocument(new
        {
            eventType = "run.started",
            runId,
            correlationId,
            startedAt,
            timestamp = DateTimeOffset.UtcNow
        }, DeterministicJsonOptions.Standard);
        _eventStore.AppendEvent(startedEventDoc.RootElement);

        // Update runs index
        UpdateRunsIndex(runId, "started", startedAt, null);

        // Save to repository
        await _runRepository.SaveAsync(runContext, ct);

        return runContext;
    }

    /// <inheritdoc />
    public async Task AttachInputAsync(string runId, WorkflowIntent intent, CancellationToken ct = default)
    {
        var evidenceFolder = GetEvidenceFolderPath(runId);
        var inputPath = Path.Combine(evidenceFolder, "input.json");

        var inputRecord = new
        {
            runId,
            correlationId = intent.CorrelationId,
            inputRaw = intent.InputRaw,
            inputNormalized = intent.InputNormalized,
            attachedAt = DateTimeOffset.UtcNow
        };

        _jsonSerializer.WriteAtomic(inputPath, inputRecord, DeterministicJsonOptions.Standard, writeIndented: true);
    }

    /// <inheritdoc />
    public async Task FinishRunAsync(string runId, bool success, Dictionary<string, object>? outputs, CancellationToken ct = default)
    {
        var evidenceFolder = GetEvidenceFolderPath(runId);
        var completedAt = DateTimeOffset.UtcNow;

        // Write commands.json using deterministic JSON
        var commandsPath = Path.Combine(evidenceFolder, "commands.json");
        var commandsRecord = new
        {
            runId,
            commands = _commandRecords.Select(c => new
            {
                c.Group,
                c.Command,
                c.Status,
                c.Timestamp
            }).ToList()
        };
        _jsonSerializer.WriteAtomic(commandsPath, commandsRecord, DeterministicJsonOptions.Standard, writeIndented: true);

        // Write summary.json using deterministic JSON
        var summaryPath = Path.Combine(evidenceFolder, "summary.json");
        var summary = new
        {
            runId,
            status = success ? "completed" : "failed",
            completedAt,
            outputs = outputs ?? new Dictionary<string, object>()
        };
        _jsonSerializer.WriteAtomic(summaryPath, summary, DeterministicJsonOptions.Standard, writeIndented: true);

        // Update run.json with final status using deterministic JSON
        var runJsonPath = Path.Combine(evidenceFolder, "run.json");
        var runMetadata = new
        {
            schemaVersion = 1,
            runId,
            status = success ? "completed" : "failed",
            completedAt
        };
        _jsonSerializer.WriteAtomic(runJsonPath, runMetadata, DeterministicJsonOptions.Standard, writeIndented: true);

        // Append run finished event to events.ndjson using EventStore
        using var finishedEventDoc = JsonSerializer.SerializeToDocument(new
        {
            eventType = success ? "run.completed" : "run.failed",
            runId,
            status = success ? "completed" : "failed",
            completedAt,
            timestamp = DateTimeOffset.UtcNow
        }, DeterministicJsonOptions.Standard);
        _eventStore.AppendEvent(finishedEventDoc.RootElement);

        // Update runs index with finished status
        UpdateRunsIndex(runId, success ? "completed" : "failed", default, completedAt);

        // Save run response to repository
        var runResponse = new RunResponse
        {
            RunId = runId,
            CorrelationId = string.Empty,
            Success = success,
            Outputs = outputs ?? new Dictionary<string, object>(),
            StartedAt = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1),
            CompletedAt = completedAt
        };

        await _runRepository.SaveAsync(runResponse, ct);
    }

    /// <inheritdoc />
    public Task RecordCommandAsync(string runId, string group, string command, string status, CancellationToken ct = default)
    {
        _commandRecords.Add(new CommandRecord
        {
            Group = group,
            Command = command,
            Status = status,
            Timestamp = DateTimeOffset.UtcNow
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task UpdateRunMetadataAsync(string runId, Dictionary<string, object> metadata, CancellationToken ct = default)
    {
        var evidenceFolder = GetEvidenceFolderPath(runId);
        var runJsonPath = Path.Combine(evidenceFolder, "run.json");

        // Load existing metadata if it exists
        var existingMetadata = new Dictionary<string, object>();
        if (File.Exists(runJsonPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(runJsonPath, ct);
                existingMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            }
            catch
            {
                // Fallback to empty if parsing fails
            }
        }

        // Update with new metadata
        foreach (var kvp in metadata)
        {
            existingMetadata[kvp.Key] = kvp.Value;
        }

        // Ensure runId and schemaVersion are preserved/set
        existingMetadata["runId"] = runId;
        if (!existingMetadata.ContainsKey("schemaVersion"))
        {
            existingMetadata["schemaVersion"] = 1;
        }

        _jsonSerializer.WriteAtomic(runJsonPath, existingMetadata, DeterministicJsonOptions.Standard, writeIndented: true);
    }

    private string GetEvidenceFolderPath(string runId)
    {
        var aosRoot = _workspace.AosRootPath;
        return Path.Combine(aosRoot, "evidence", "runs", runId);
    }

    private static string GenerateRunId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"RUN-{timestamp}-{random}";
    }

    private void UpdateRunsIndex(string runId, string status, DateTimeOffset startedAt, DateTimeOffset? completedAt)
    {
        var aosRoot = _workspace.AosRootPath;
        var indexPath = Path.Combine(aosRoot, "evidence", "runs", "index.json");

        // Ensure directory exists
        var dir = Path.GetDirectoryName(indexPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Read existing index or create new one
        var index = new List<RunIndexItem>();
        if (File.Exists(indexPath))
        {
            try
            {
                var json = File.ReadAllText(indexPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("items", out var itemsElement) &&
                    itemsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsElement.EnumerateArray())
                    {
                        var itemRunId = item.GetProperty("runId").GetString() ?? "";
                        var itemStatus = item.GetProperty("status").GetString() ?? "";
                        var itemStartedAt = item.TryGetProperty("startedAt", out var startedEl) && startedEl.ValueKind == JsonValueKind.String
                            ? DateTimeOffset.Parse(startedEl.GetString()!)
                            : DateTimeOffset.MinValue;
                        var itemCompletedAt = item.TryGetProperty("completedAt", out var completedEl) && completedEl.ValueKind == JsonValueKind.String
                            ? DateTimeOffset.Parse(completedEl.GetString()!)
                            : (DateTimeOffset?)null;

                        index.Add(new RunIndexItem
                        {
                            RunId = itemRunId,
                            Status = itemStatus,
                            StartedAt = itemStartedAt,
                            CompletedAt = itemCompletedAt
                        });
                    }
                }
            }
            catch
            {
                // If reading fails, start with empty index
                index = new List<RunIndexItem>();
            }
        }

        // Find and update or add the run entry
        var existing = index.FirstOrDefault(i => i.RunId == runId);
        if (existing != null)
        {
            existing.Status = status;
            if (completedAt.HasValue)
            {
                existing.CompletedAt = completedAt.Value;
            }
        }
        else
        {
            index.Add(new RunIndexItem
            {
                RunId = runId,
                Status = status,
                StartedAt = startedAt == default ? DateTimeOffset.UtcNow : startedAt,
                CompletedAt = completedAt
            });
        }

        // Sort by runId for deterministic ordering
        index = index.OrderBy(i => i.RunId, StringComparer.Ordinal).ToList();

        // Write updated index using deterministic JSON
        var indexDoc = new
        {
            schemaVersion = 1,
            items = index.Select(i => new
            {
                i.RunId,
                i.Status,
                startedAt = i.StartedAt.ToString("O"),
                completedAt = i.CompletedAt?.ToString("O")
            }).ToList()
        };

        _jsonSerializer.WriteAtomic(indexPath, indexDoc, DeterministicJsonOptions.Standard, writeIndented: true);
    }

    private sealed class RunIndexItem
    {
        public required string RunId { get; set; }
        public required string Status { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }

    private sealed class CommandRecord
    {
        public required string Group { get; init; }
        public required string Command { get; init; }
        public required string Status { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }
}

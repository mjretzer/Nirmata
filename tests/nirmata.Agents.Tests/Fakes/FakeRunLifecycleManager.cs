using System.Text.Json;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Persistence.Runs;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IRunLifecycleManager for unit testing.
/// This version creates actual evidence folder structure for E2E tests.
/// </summary>
public sealed class FakeRunLifecycleManager : IRunLifecycleManager, IDisposable
{
    private readonly List<CommandRecord> _commandRecords = new();
    private readonly Dictionary<string, RunContext> _runs = new();
    private readonly Dictionary<string, WorkflowIntent> _inputs = new();
    private string? _currentRunId;
    private readonly string? _basePath;
    private readonly bool _useRealFilesystem;

    /// <summary>
    /// Creates a fake that operates in-memory only.
    /// </summary>
    public FakeRunLifecycleManager()
    {
        _useRealFilesystem = false;
        _basePath = null;
    }

    /// <summary>
    /// Creates a fake that writes to the filesystem for E2E tests.
    /// </summary>
    public FakeRunLifecycleManager(string basePath)
    {
        _basePath = basePath;
        _useRealFilesystem = true;
    }

    /// <summary>
    /// Gets the current run context, or null if no run is active.
    /// </summary>
    public RunContext? CurrentRun => _currentRunId != null && _runs.TryGetValue(_currentRunId, out var context) ? context : null;

    /// <summary>
    /// Gets all recorded commands for verification.
    /// </summary>
    public IReadOnlyList<CommandRecord> RecordedCommands => _commandRecords.AsReadOnly();

    /// <summary>
    /// Gets a run context by run ID.
    /// </summary>
    public RunContext? GetRun(string runId) => _runs.TryGetValue(runId, out var context) ? context : null;

    /// <summary>
    /// Seeds a run context for testing.
    /// </summary>
    public void SeedRun(string runId)
    {
        var context = new RunContext
        {
            RunId = runId,
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowName = "test-workflow",
            StartedAt = DateTimeOffset.UtcNow,
            CurrentStep = "started"
        };
        _runs[runId] = context;
        
        if (_useRealFilesystem && _basePath != null)
        {
            CreateEvidenceStructure(runId);
        }
    }

    /// <inheritdoc />
    public Task<RunContext> StartRunAsync(CancellationToken ct = default)
    {
        var runId = $"RUN-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var context = new RunContext
        {
            RunId = runId,
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowName = "orchestrator",
            StartedAt = DateTimeOffset.UtcNow,
            CurrentStep = "started"
        };
        _runs[runId] = context;
        _currentRunId = runId;

        if (_useRealFilesystem && _basePath != null)
        {
            CreateEvidenceStructure(runId);
        }

        return Task.FromResult(context);
    }

    private void CreateEvidenceStructure(string runId)
    {
        var evidencePath = GetEvidencePath(runId);
        Directory.CreateDirectory(evidencePath);
        Directory.CreateDirectory(Path.Combine(evidencePath, "logs"));
        Directory.CreateDirectory(Path.Combine(evidencePath, "artifacts"));

        // Create initial run.json
        var runJson = new
        {
            schemaVersion = 1,
            runId,
            status = "started",
            startedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        File.WriteAllText(Path.Combine(evidencePath, "run.json"), JsonSerializer.Serialize(runJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
    }

    private string GetEvidencePath(string runId)
    {
        return Path.Combine(_basePath!, ".aos", "evidence", "runs", runId);
    }

    /// <inheritdoc />
    public Task AttachInputAsync(string runId, WorkflowIntent intent, CancellationToken ct = default)
    {
        if (_runs.TryGetValue(runId, out var context))
        {
            context.Inputs["intent"] = intent;
            context.CurrentStep = "input-attached";
            _inputs[runId] = intent;

            if (_useRealFilesystem && _basePath != null)
            {
                WriteInputJson(runId, intent);
            }
        }
        return Task.CompletedTask;
    }

    private void WriteInputJson(string runId, WorkflowIntent intent)
    {
        var evidencePath = GetEvidencePath(runId);
        var inputJson = new
        {
            inputRaw = intent.InputRaw,
            inputNormalized = intent.InputNormalized,
            correlationId = intent.CorrelationId,
            runId,
            attachedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        File.WriteAllText(Path.Combine(evidencePath, "input.json"), JsonSerializer.Serialize(inputJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
    }

    /// <inheritdoc />
    public Task FinishRunAsync(string runId, bool success, Dictionary<string, object>? outputs, CancellationToken ct = default)
    {
        if (_runs.TryGetValue(runId, out var context))
        {
            context.CurrentStep = success ? "completed" : "failed";
            if (outputs != null)
            {
                foreach (var (key, value) in outputs)
                {
                    context.Metadata[$"output_{key}"] = value?.ToString() ?? string.Empty;
                }
            }

            if (_useRealFilesystem && _basePath != null)
            {
                WriteCloseRunArtifacts(runId, success, outputs);
                UpdateRunIndex(runId, success);
            }
        }
        if (_currentRunId == runId)
        {
            _currentRunId = null;
        }
        return Task.CompletedTask;
    }

    private void WriteCloseRunArtifacts(string runId, bool success, Dictionary<string, object>? outputs)
    {
        var evidencePath = GetEvidencePath(runId);

        // Update run.json with completed status
        var runJson = new
        {
            schemaVersion = 1,
            runId,
            status = success ? "completed" : "failed",
            startedAt = _runs[runId].StartedAt.ToString("O"),
            completedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        File.WriteAllText(Path.Combine(evidencePath, "run.json"), JsonSerializer.Serialize(runJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));

        // Write commands.json
        var commands = _commandRecords.Where(c => c.RunId == runId).ToList();
        var commandsJson = new
        {
            schemaVersion = 1,
            runId,
            commands = commands.Select(c => new
            {
                group = c.Group,
                command = c.Command,
                status = c.Status,
                timestamp = c.Timestamp.ToString("O")
            }).ToList()
        };
        File.WriteAllText(Path.Combine(evidencePath, "commands.json"), JsonSerializer.Serialize(commandsJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));

        // Write summary.json
        var summaryJson = new
        {
            schemaVersion = 1,
            runId,
            status = success ? "completed" : "failed",
            completedAt = DateTimeOffset.UtcNow.ToString("O"),
            outputs = outputs ?? new Dictionary<string, object>()
        };
        File.WriteAllText(Path.Combine(evidencePath, "summary.json"), JsonSerializer.Serialize(summaryJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
    }

    private void UpdateRunIndex(string runId, bool success)
    {
        var runsPath = Path.Combine(_basePath!, ".aos", "evidence", "runs");
        Directory.CreateDirectory(runsPath);
        var indexPath = Path.Combine(runsPath, "index.json");

        List<IndexEntry> entries = new();
        if (File.Exists(indexPath))
        {
            var existingJson = File.ReadAllText(indexPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var existingDoc = JsonSerializer.Deserialize<IndexDocument>(existingJson, options);
            if (existingDoc?.Items != null)
            {
                entries.AddRange(existingDoc.Items);
            }
        }

        // Add or update entry for this run
        var existingEntry = entries.FirstOrDefault(e => e.RunId == runId);
        if (existingEntry != null)
        {
            entries.Remove(existingEntry);
        }
        entries.Add(new IndexEntry
        {
            RunId = runId,
            Status = success ? "completed" : "failed",
            StartedAt = _runs[runId].StartedAt.ToString("O"),
            FinishedAt = DateTimeOffset.UtcNow.ToString("O")
        });

        var newIndex = new IndexDocument
        {
            SchemaVersion = 1,
            Items = entries.ToArray()
        };
        File.WriteAllText(indexPath, JsonSerializer.Serialize(newIndex, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
    }

    /// <inheritdoc />
    public Task RecordCommandAsync(string runId, string group, string command, string status, CancellationToken ct = default)
    {
        Console.WriteLine($"[FakeRunLifecycleManager] Recording command: RunId={runId}, Group={group}, Command={command}, Status={status}");
        if (_runs.ContainsKey(runId))
        {
            _commandRecords.Add(new CommandRecord
            {
                RunId = runId,
                Group = group,
                Command = command,
                Status = status,
                Timestamp = DateTimeOffset.UtcNow
            });
            if (_runs.TryGetValue(runId, out var context))
            {
                context.CurrentStep = $"command-{command}";
            }
            Console.WriteLine($"[FakeRunLifecycleManager] Command recorded. Total records: {_commandRecords.Count}");
        }
        else
        {
            Console.WriteLine($"[FakeRunLifecycleManager] RunId {runId} not found in _runs. Available keys: {string.Join(", ", _runs.Keys)}");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateRunMetadataAsync(string runId, Dictionary<string, object> metadata, CancellationToken ct = default)
    {
        if (_runs.TryGetValue(runId, out var context))
        {
            foreach (var kvp in metadata)
            {
                context.Metadata[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all recorded commands for verification.
    /// </summary>
    public IReadOnlyList<CommandRecord> GetRecordedCommands() => _commandRecords.AsReadOnly();

    /// <summary>
    /// Resets the fake, clearing all recorded commands.
    /// </summary>
    public void Reset()
    {
        _commandRecords.Clear();
        _runs.Clear();
        _inputs.Clear();
        _currentRunId = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Reset();
    }

    private class IndexDocument
    {
        public int SchemaVersion { get; set; }
        public IndexEntry[] Items { get; set; } = Array.Empty<IndexEntry>();
    }

    private class IndexEntry
    {
        public string RunId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StartedAt { get; set; } = string.Empty;
        public string FinishedAt { get; set; } = string.Empty;
    }

    /// <summary>
    /// Record of a command for verification.
    /// </summary>
    public sealed class CommandRecord
    {
        public required string RunId { get; init; }
        public required string Group { get; init; }
        public required string Command { get; init; }
        public required string Status { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Data.Dto.Models.State;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Reads workspace state artifacts from <c>.aos/state/</c> and maps them to API DTOs.
/// All methods are resilient to missing files or malformed JSON — they return <c>null</c>
/// or empty collections rather than throwing so callers always get a usable result.
/// </summary>
public sealed class StateService : IStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ContinuityStateDto?> GetStateAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var stateFile = Path.Combine(workspaceRoot, ".aos", "state", "state.json");
        if (!File.Exists(stateFile))
            return null;

        StateFileModel? model;
        try
        {
            var json = await File.ReadAllTextAsync(stateFile, cancellationToken);
            model = JsonSerializer.Deserialize<StateFileModel>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }

        if (model is null)
            return null;

        return new ContinuityStateDto
        {
            Position = model.Position is null ? null : new StatePositionDto
            {
                MilestoneId = model.Position.MilestoneId,
                PhaseId = model.Position.PhaseId,
                TaskId = model.Position.TaskId,
                StepIndex = model.Position.StepIndex,
                Status = model.Position.Status,
            },
            Decisions = (model.Decisions ?? [])
                .Select(d => new StateDecisionDto
                {
                    Id = d.Id,
                    Topic = d.Topic,
                    Decision = d.Decision,
                    Rationale = d.Rationale,
                    Timestamp = d.Timestamp,
                })
                .ToList(),
            Blockers = (model.Blockers ?? [])
                .Select(b => new StateBlockerDto
                {
                    Id = b.Id,
                    Description = b.Description,
                    AffectedTask = b.AffectedTask,
                    Timestamp = b.Timestamp,
                })
                .ToList(),
            LastTransition = model.LastTransition is null ? null : new StateTransitionDto
            {
                From = model.LastTransition.From,
                To = model.LastTransition.To,
                Timestamp = model.LastTransition.Timestamp,
                Trigger = model.LastTransition.Trigger,
            },
        };
    }

    public async Task<HandoffSnapshotDto?> GetHandoffAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var handoffFile = Path.Combine(workspaceRoot, ".aos", "state", "handoff.json");
        if (!File.Exists(handoffFile))
            return null;

        HandoffFileModel? model;
        try
        {
            var json = await File.ReadAllTextAsync(handoffFile, cancellationToken);
            model = JsonSerializer.Deserialize<HandoffFileModel>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }

        if (model is null)
            return null;

        return new HandoffSnapshotDto
        {
            Cursor = model.Cursor is null ? null : new StatePositionDto
            {
                MilestoneId = model.Cursor.MilestoneId,
                PhaseId = model.Cursor.PhaseId,
                TaskId = model.Cursor.TaskId,
                StepIndex = model.Cursor.StepIndex,
                Status = model.Cursor.Status,
            },
            InFlightTask = model.InFlightTask,
            InFlightStep = model.InFlightStep,
            AllowedScope = model.AllowedScope ?? [],
            PendingVerification = model.PendingVerification,
            NextCommand = model.NextCommand,
            Timestamp = model.Timestamp,
        };
    }

    public async Task<IReadOnlyList<StateEventDto>> GetEventsAsync(
        string workspaceRoot, int limit = 50, CancellationToken cancellationToken = default)
    {
        var eventsFile = Path.Combine(workspaceRoot, ".aos", "state", "events.ndjson");
        if (!File.Exists(eventsFile))
            return [];

        var lines = new List<string>();
        try
        {
            // Read the NDJSON file line-by-line and keep only the tail we need.
            await foreach (var line in ReadLinesAsync(eventsFile, cancellationToken))
                lines.Add(line);
        }
        catch (IOException)
        {
            return [];
        }

        var tail = lines.Count <= limit
            ? lines
            : lines.GetRange(lines.Count - limit, limit);

        var events = new List<StateEventDto>(tail.Count);
        foreach (var line in tail)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            EventLineModel? ev;
            try
            {
                ev = JsonSerializer.Deserialize<EventLineModel>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue; // skip malformed lines
            }

            if (ev is null)
                continue;

            events.Add(new StateEventDto
            {
                Type = ev.Type,
                Timestamp = ev.Timestamp,
                Payload = ev.Payload?.GetRawText(),
                References = ev.References ?? [],
            });
        }

        return events;
    }

    public async Task<IReadOnlyList<CheckpointSummaryDto>> GetCheckpointsAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var checkpointsDir = Path.Combine(workspaceRoot, ".aos", "state", "checkpoints");
        if (!Directory.Exists(checkpointsDir))
            return [];

        string[] files;
        try
        {
            files = Directory.GetFiles(checkpointsDir, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return [];
        }

        var results = new List<CheckpointSummaryDto>(files.Length);
        foreach (var file in files.OrderByDescending(f => f))
        {
            CheckpointFileModel? model;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                model = JsonSerializer.Deserialize<CheckpointFileModel>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                continue;
            }

            var id = Path.GetFileNameWithoutExtension(file);
            results.Add(new CheckpointSummaryDto
            {
                Id = id,
                Position = model?.Position is null ? null : new StatePositionDto
                {
                    MilestoneId = model.Position.MilestoneId,
                    PhaseId = model.Position.PhaseId,
                    TaskId = model.Position.TaskId,
                    StepIndex = model.Position.StepIndex,
                    Status = model.Position.Status,
                },
                Timestamp = model?.Timestamp,
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<ContextPackSummaryDto>> GetContextPacksAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var packsDir = Path.Combine(workspaceRoot, ".aos", "context", "packs");
        if (!Directory.Exists(packsDir))
            return [];

        string[] files;
        try
        {
            files = Directory.GetFiles(packsDir, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return [];
        }

        var results = new List<ContextPackSummaryDto>(files.Length);
        foreach (var file in files.OrderBy(f => f))
        {
            ContextPackFileModel? model;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                model = JsonSerializer.Deserialize<ContextPackFileModel>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                continue;
            }

            var packId = model?.PackId ?? Path.GetFileNameWithoutExtension(file);
            results.Add(new ContextPackSummaryDto
            {
                PackId = packId,
                Mode = model?.Mode,
                BudgetTokens = model?.BudgetTokens,
                ArtifactCount = model?.Artifacts?.Count ?? 0,
            });
        }

        return results;
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(path);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            yield return line;
    }

    // ── Private JSON deserialization models ───────────────────────────────────
    // Mirror the AOS state.json schema (documents/architecture/schemas.md).

    private sealed class StateFileModel
    {
        public PositionModel? Position { get; init; }
        public IReadOnlyList<DecisionModel>? Decisions { get; init; }
        public IReadOnlyList<BlockerModel>? Blockers { get; init; }
        [JsonPropertyName("lastTransition")]
        public TransitionModel? LastTransition { get; init; }
    }

    private sealed class PositionModel
    {
        public string? MilestoneId { get; init; }
        public string? PhaseId { get; init; }
        public string? TaskId { get; init; }
        public int? StepIndex { get; init; }
        public string? Status { get; init; }
    }

    private sealed class DecisionModel
    {
        public string? Id { get; init; }
        public string? Topic { get; init; }
        public string? Decision { get; init; }
        public string? Rationale { get; init; }
        public DateTimeOffset? Timestamp { get; init; }
    }

    private sealed class BlockerModel
    {
        public string? Id { get; init; }
        public string? Description { get; init; }
        public string? AffectedTask { get; init; }
        public DateTimeOffset? Timestamp { get; init; }
    }

    private sealed class TransitionModel
    {
        public string? From { get; init; }
        public string? To { get; init; }
        public DateTimeOffset? Timestamp { get; init; }
        public string? Trigger { get; init; }
    }

    private sealed class HandoffFileModel
    {
        public PositionModel? Cursor { get; init; }
        [JsonPropertyName("inFlightTask")]
        public string? InFlightTask { get; init; }
        [JsonPropertyName("inFlightStep")]
        public int? InFlightStep { get; init; }
        [JsonPropertyName("allowedScope")]
        public IReadOnlyList<string>? AllowedScope { get; init; }
        [JsonPropertyName("pendingVerification")]
        public bool PendingVerification { get; init; }
        [JsonPropertyName("nextCommand")]
        public string? NextCommand { get; init; }
        public DateTimeOffset? Timestamp { get; init; }
    }

    private sealed class EventLineModel
    {
        public string? Type { get; init; }
        public DateTimeOffset? Timestamp { get; init; }
        public System.Text.Json.JsonElement? Payload { get; init; }
        public IReadOnlyList<string>? References { get; init; }
    }

    private sealed class CheckpointFileModel
    {
        public PositionModel? Position { get; init; }
        public DateTimeOffset? Timestamp { get; init; }
    }

    private sealed class ContextPackFileModel
    {
        [JsonPropertyName("packId")]
        public string? PackId { get; init; }
        public string? Mode { get; init; }
        [JsonPropertyName("budgetTokens")]
        public int? BudgetTokens { get; init; }
        public IReadOnlyList<System.Text.Json.JsonElement>? Artifacts { get; init; }
    }
}

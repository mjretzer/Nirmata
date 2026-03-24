using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Data.Dto.Models.Spec;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Reads workspace spec artifacts from <c>.aos/spec/</c> and maps them to API DTOs.
/// All methods are resilient to missing directories or malformed files — they skip unreadable entries
/// rather than throwing, so callers always get a valid (possibly empty) result.
/// </summary>
public sealed class SpecService : ISpecService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<MilestoneSummaryDto>> GetMilestonesAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var milestonesDir = Path.Combine(workspaceRoot, ".aos", "spec", "milestones");
        if (!Directory.Exists(milestonesDir))
            return [];

        var results = new List<MilestoneSummaryDto>();

        foreach (var dir in Directory.EnumerateDirectories(milestonesDir).Order())
        {
            var file = Path.Combine(dir, "milestone.json");
            if (!File.Exists(file))
                continue;

            MilestoneFileModel? model;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                model = JsonSerializer.Deserialize<MilestoneFileModel>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                continue;
            }

            if (model is null)
                continue;

            results.Add(new MilestoneSummaryDto
            {
                Id = model.Id ?? Path.GetFileName(dir),
                Title = model.Title ?? string.Empty,
                Status = model.Status ?? string.Empty,
                PhaseIds = model.Phases ?? [],
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<PhaseSummaryDto>> GetPhasesAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var phasesDir = Path.Combine(workspaceRoot, ".aos", "spec", "phases");
        if (!Directory.Exists(phasesDir))
            return [];

        var results = new List<PhaseSummaryDto>();
        var order = 0;

        foreach (var dir in Directory.EnumerateDirectories(phasesDir).Order())
        {
            var file = Path.Combine(dir, "phase.json");
            if (!File.Exists(file))
                continue;

            PhaseFileModel? model;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                model = JsonSerializer.Deserialize<PhaseFileModel>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                continue;
            }

            if (model is null)
                continue;

            // Task IDs may be stored as "tasks", "taskIds", or "taskRefs" in the JSON file.
            // The private model covers the common variants; the first non-null wins.
            var taskIds = model.TaskIds ?? model.Tasks ?? model.TaskRefs ?? [];

            results.Add(new PhaseSummaryDto
            {
                Id = model.Id ?? Path.GetFileName(dir),
                MilestoneId = model.MilestoneId ?? model.Milestone ?? string.Empty,
                Title = model.Title ?? string.Empty,
                Status = model.Status ?? string.Empty,
                Order = model.Order ?? order,
                TaskIds = taskIds,
            });

            order++;
        }

        return results;
    }

    public async Task<IReadOnlyList<TaskSummaryDto>> GetTasksAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var tasksDir = Path.Combine(workspaceRoot, ".aos", "spec", "tasks");
        if (!Directory.Exists(tasksDir))
            return [];

        var results = new List<TaskSummaryDto>();

        foreach (var dir in Directory.EnumerateDirectories(tasksDir).Order())
        {
            var file = Path.Combine(dir, "task.json");
            if (!File.Exists(file))
                continue;

            TaskFileModel? model;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                model = JsonSerializer.Deserialize<TaskFileModel>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                continue;
            }

            if (model is null)
                continue;

            results.Add(new TaskSummaryDto
            {
                Id = model.Id ?? Path.GetFileName(dir),
                PhaseId = model.PhaseId ?? model.PhaseRef ?? model.Phase ?? string.Empty,
                MilestoneId = model.MilestoneId ?? model.MilestoneRef ?? model.Milestone ?? string.Empty,
                Title = model.Title ?? string.Empty,
                Status = model.Status ?? string.Empty,
            });
        }

        return results;
    }

    public async Task<ProjectSpecDto?> GetProjectAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var projectFile = Path.Combine(workspaceRoot, ".aos", "spec", "project.json");
        if (!File.Exists(projectFile))
            return null;

        ProjectFileModel? model;
        try
        {
            var json = await File.ReadAllTextAsync(projectFile, cancellationToken);
            model = JsonSerializer.Deserialize<ProjectFileModel>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }

        if (model is null)
            return null;

        return new ProjectSpecDto
        {
            Name = model.Name,
            Description = model.Description,
            Version = model.Version,
            Owner = model.Owner,
            Repo = model.Repo,
            Milestones = model.Milestones ?? [],
            Constraints = model.Constraints ?? [],
            Tags = model.Tags ?? [],
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
        };
    }

    // ── Private JSON deserialization models ──────────────────────────────────
    // These mirror the AOS artifact schemas (documents/architecture/schemas.md).
    // Nullable members handle missing fields gracefully; callers apply fallbacks.

    private sealed class MilestoneFileModel
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Status { get; init; }
        /// <summary>Array of phase IDs belonging to this milestone (JSON key: "phases").</summary>
        public IReadOnlyList<string>? Phases { get; init; }
    }

    private sealed class PhaseFileModel
    {
        public string? Id { get; init; }
        // milestone reference — AOS files may use any of these names
        public string? MilestoneId { get; init; }
        public string? Milestone { get; init; }
        public string? Title { get; init; }
        public string? Status { get; init; }
        public int? Order { get; init; }
        // task references — AOS files may use any of these names
        [JsonPropertyName("taskIds")]
        public IReadOnlyList<string>? TaskIds { get; init; }
        public IReadOnlyList<string>? Tasks { get; init; }
        public IReadOnlyList<string>? TaskRefs { get; init; }
    }

    private sealed class TaskFileModel
    {
        public string? Id { get; init; }
        // phase reference — AOS files may use any of these names
        public string? PhaseId { get; init; }
        public string? PhaseRef { get; init; }
        public string? Phase { get; init; }
        // milestone reference — AOS files may use any of these names
        public string? MilestoneId { get; init; }
        public string? MilestoneRef { get; init; }
        public string? Milestone { get; init; }
        public string? Title { get; init; }
        public string? Status { get; init; }
    }

    private sealed class ProjectFileModel
    {
        public string? Name { get; init; }
        public string? Description { get; init; }
        public string? Version { get; init; }
        public string? Owner { get; init; }
        public string? Repo { get; init; }
        public IReadOnlyList<string>? Milestones { get; init; }
        public IReadOnlyList<string>? Constraints { get; init; }
        public IReadOnlyList<string>? Tags { get; init; }
        public DateTimeOffset? CreatedAt { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}

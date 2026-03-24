using System.Text.Json;
using nirmata.Data.Dto.Models.Spec;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Reads UAT records from <c>.aos/spec/uat/</c> (global records) and
/// <c>.aos/spec/tasks/TSK-*/uat.json</c> (task-level records), then derives
/// per-task and per-phase pass/fail summaries.
/// All methods are resilient to missing directories or malformed files.
/// </summary>
public sealed class UatService : IUatService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static string UatDir(string workspaceRoot) =>
        Path.Combine(workspaceRoot, ".aos", "spec", "uat");

    private static string TasksDir(string workspaceRoot) =>
        Path.Combine(workspaceRoot, ".aos", "spec", "tasks");

    public async Task<UatSummaryDto> GetSummaryAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        var records = new List<UatRecordDto>();

        // Read global UAT records from .aos/spec/uat/UAT-*.json
        var uatDir = UatDir(workspaceRoot);
        if (Directory.Exists(uatDir))
        {
            foreach (var file in Directory.EnumerateFiles(uatDir, "UAT-*.json").Order())
            {
                var dto = await ReadUatFileAsync(file, Path.GetFileNameWithoutExtension(file), cancellationToken);
                if (dto is not null)
                    records.Add(dto);
            }
        }

        // Read task-level UAT records from .aos/spec/tasks/TSK-*/uat.json
        var tasksDir = TasksDir(workspaceRoot);
        if (Directory.Exists(tasksDir))
        {
            foreach (var taskDir in Directory.EnumerateDirectories(tasksDir, "TSK-*").Order())
            {
                var uatFile = Path.Combine(taskDir, "uat.json");
                if (!File.Exists(uatFile))
                    continue;

                var fallbackId = Path.GetFileName(taskDir); // e.g. "TSK-000001"
                var dto = await ReadUatFileAsync(uatFile, fallbackId, cancellationToken);
                if (dto is not null)
                    records.Add(dto);
            }
        }

        var taskSummaries = DeriveTaskSummaries(records);
        var phaseSummaries = DerivePhaseSummaries(records, taskSummaries);

        return new UatSummaryDto
        {
            Records = records,
            TaskSummaries = taskSummaries,
            PhaseSummaries = phaseSummaries,
        };
    }

    // ── Derivation helpers ──────────────────────────────────────────────────────

    private static IReadOnlyList<UatTaskSummaryDto> DeriveTaskSummaries(IReadOnlyList<UatRecordDto> records)
    {
        var byTask = records
            .Where(r => r.TaskId is not null)
            .GroupBy(r => r.TaskId!, StringComparer.OrdinalIgnoreCase);

        var summaries = new List<UatTaskSummaryDto>();
        foreach (var group in byTask)
        {
            var taskRecords = group.ToList();
            var status = DeriveStatus(taskRecords.Select(r => r.Status));
            summaries.Add(new UatTaskSummaryDto
            {
                TaskId = group.Key,
                Status = status,
                RecordCount = taskRecords.Count,
            });
        }

        return summaries.OrderBy(s => s.TaskId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<UatPhaseSummaryDto> DerivePhaseSummaries(
        IReadOnlyList<UatRecordDto> records,
        IReadOnlyList<UatTaskSummaryDto> taskSummaries)
    {
        // Collect phaseId from records that carry one directly
        var phaseToTaskIds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records.Where(r => r.PhaseId is not null))
        {
            if (!phaseToTaskIds.TryGetValue(record.PhaseId!, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                phaseToTaskIds[record.PhaseId!] = set;
            }

            if (record.TaskId is not null)
                set.Add(record.TaskId);
        }

        var summaries = new List<UatPhaseSummaryDto>();
        foreach (var (phaseId, taskIds) in phaseToTaskIds)
        {
            var relevantTaskStatuses = taskSummaries
                .Where(ts => taskIds.Contains(ts.TaskId))
                .Select(ts => ts.Status);

            var status = DeriveStatus(relevantTaskStatuses);

            summaries.Add(new UatPhaseSummaryDto
            {
                PhaseId = phaseId,
                Status = status,
                TaskIds = taskIds.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList(),
            });
        }

        return summaries.OrderBy(s => s.PhaseId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Returns "failed" if any status is "failed", "passed" if all are "passed",
    /// otherwise "unknown".
    /// </summary>
    private static string DeriveStatus(IEnumerable<string> statuses)
    {
        var all = statuses.ToList();
        if (all.Count == 0)
            return "unknown";

        if (all.Any(s => string.Equals(s, "failed", StringComparison.OrdinalIgnoreCase)))
            return "failed";

        if (all.All(s => string.Equals(s, "passed", StringComparison.OrdinalIgnoreCase)))
            return "passed";

        return "unknown";
    }

    // ── File reading ────────────────────────────────────────────────────────────

    private static async Task<UatRecordDto?> ReadUatFileAsync(
        string path, string fallbackId, CancellationToken cancellationToken)
    {
        UatFileModel? model;
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            model = JsonSerializer.Deserialize<UatFileModel>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }

        if (model is null)
            return null;

        return ToDto(model, fallbackId);
    }

    private static UatRecordDto ToDto(UatFileModel model, string fallbackId) => new()
    {
        Id = model.Id ?? fallbackId,
        TaskId = model.TaskId ?? model.Task,
        PhaseId = model.PhaseId ?? model.Phase,
        Status = model.Status ?? "unknown",
        Observations = model.Observations,
        ReproSteps = model.ReproSteps ?? model.Repro,
        Checks = model.Checks?.Select(c => new UatCheckDto
        {
            CriterionId = c.CriterionId ?? c.Id ?? string.Empty,
            Passed = c.Passed,
            Message = c.Message,
            CheckType = c.CheckType,
            TargetPath = c.TargetPath,
            Expected = c.Expected,
            Actual = c.Actual,
        }).ToList() ?? [],
    };

    // ── On-disk JSON deserialization model ────────────────────────────────────
    // Mirrors uat.schema.json. Nullable members tolerate missing or renamed fields.

    private sealed record UatFileModel
    {
        public string? Id { get; init; }
        public string? TaskId { get; init; }
        public string? Task { get; init; }
        public string? PhaseId { get; init; }
        public string? Phase { get; init; }
        public string? Status { get; init; }
        public string? Observations { get; init; }
        public string? ReproSteps { get; init; }
        public string? Repro { get; init; }
        public List<UatCheckFileModel>? Checks { get; init; }
    }

    private sealed record UatCheckFileModel
    {
        public string? CriterionId { get; init; }
        public string? Id { get; init; }
        public bool Passed { get; init; }
        public string? Message { get; init; }
        public string? CheckType { get; init; }
        public string? TargetPath { get; init; }
        public string? Expected { get; init; }
        public string? Actual { get; init; }
    }
}

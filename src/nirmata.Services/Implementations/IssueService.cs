using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Data.Dto.Models.Spec;
using nirmata.Data.Dto.Requests.Issues;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Reads and writes workspace issue artifacts from <c>.aos/spec/issues/</c>.
/// Files are named <c>ISS-####.json</c>; each is a flat JSON record conforming to <c>issue.schema.json</c>.
/// All methods are resilient to missing directories or malformed files.
/// </summary>
public sealed class IssueService : IIssueService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static string IssuesDir(string workspaceRoot) =>
        Path.Combine(workspaceRoot, ".aos", "spec", "issues");

    private static string IssueFilePath(string workspaceRoot, string issueId) =>
        Path.Combine(IssuesDir(workspaceRoot), $"{issueId}.json");

    public async Task<IReadOnlyList<IssueDto>> GetAllAsync(
        string workspaceRoot,
        string? status = null,
        string? severity = null,
        string? phaseId = null,
        string? taskId = null,
        string? milestoneId = null,
        CancellationToken cancellationToken = default)
    {
        var dir = IssuesDir(workspaceRoot);
        if (!Directory.Exists(dir))
            return [];

        var results = new List<IssueDto>();

        foreach (var file in Directory.EnumerateFiles(dir, "ISS-*.json").Order())
        {
            var dto = await ReadIssueFileAsync(file, cancellationToken);
            if (dto is not null)
                results.Add(dto);
        }

        IEnumerable<IssueDto> filtered = results;

        if (status is not null)
            filtered = filtered.Where(i => string.Equals(i.Status, status, StringComparison.OrdinalIgnoreCase));

        if (severity is not null)
            filtered = filtered.Where(i => string.Equals(i.Severity, severity, StringComparison.OrdinalIgnoreCase));

        if (phaseId is not null)
            filtered = filtered.Where(i => string.Equals(i.PhaseId, phaseId, StringComparison.OrdinalIgnoreCase));

        if (taskId is not null)
            filtered = filtered.Where(i => string.Equals(i.TaskId, taskId, StringComparison.OrdinalIgnoreCase));

        if (milestoneId is not null)
            filtered = filtered.Where(i => string.Equals(i.MilestoneId, milestoneId, StringComparison.OrdinalIgnoreCase));

        return filtered.ToList();
    }

    public async Task<IssueDto?> GetByIdAsync(
        string workspaceRoot, string issueId, CancellationToken cancellationToken = default)
    {
        var file = IssueFilePath(workspaceRoot, issueId);
        if (!File.Exists(file))
            return null;

        return await ReadIssueFileAsync(file, cancellationToken);
    }

    public async Task<IssueDto> CreateAsync(
        string workspaceRoot, IssueCreateRequest request, CancellationToken cancellationToken = default)
    {
        var dir = IssuesDir(workspaceRoot);
        Directory.CreateDirectory(dir);

        var id = GenerateNextId(dir);
        var model = new IssueFileModel
        {
            Id = id,
            Title = request.Title,
            Status = "open",
            Severity = request.Severity,
            Scope = request.Scope,
            Repro = request.Repro,
            Expected = request.Expected,
            Actual = request.Actual,
            ImpactedFiles = request.ImpactedFiles?.ToList() ?? [],
            PhaseId = request.PhaseId,
            TaskId = request.TaskId,
            MilestoneId = request.MilestoneId,
        };

        var json = JsonSerializer.Serialize(model, JsonOptions);
        await File.WriteAllTextAsync(IssueFilePath(workspaceRoot, id), json, cancellationToken);

        return ToDto(model, id);
    }

    public async Task<IssueDto?> UpdateAsync(
        string workspaceRoot, string issueId, IssueUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var file = IssueFilePath(workspaceRoot, issueId);
        if (!File.Exists(file))
            return null;

        IssueFileModel? existing;
        try
        {
            var existingJson = await File.ReadAllTextAsync(file, cancellationToken);
            existing = JsonSerializer.Deserialize<IssueFileModel>(existingJson, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }

        if (existing is null)
            return null;

        var updated = new IssueFileModel
        {
            Id = issueId,
            Title = request.Title,
            Status = existing.Status ?? "open",
            Severity = request.Severity,
            Scope = request.Scope,
            Repro = request.Repro,
            Expected = request.Expected,
            Actual = request.Actual,
            ImpactedFiles = request.ImpactedFiles?.ToList() ?? existing.ImpactedFiles ?? [],
            PhaseId = request.PhaseId ?? existing.PhaseId ?? existing.Phase,
            TaskId = request.TaskId ?? existing.TaskId ?? existing.Task,
            MilestoneId = request.MilestoneId ?? existing.MilestoneId ?? existing.Milestone,
        };

        var json = JsonSerializer.Serialize(updated, JsonOptions);
        await File.WriteAllTextAsync(file, json, cancellationToken);

        return ToDto(updated, issueId);
    }

    public Task<bool> DeleteAsync(
        string workspaceRoot, string issueId, CancellationToken cancellationToken = default)
    {
        var file = IssueFilePath(workspaceRoot, issueId);
        if (!File.Exists(file))
            return Task.FromResult(false);

        File.Delete(file);
        return Task.FromResult(true);
    }

    public async Task<IssueDto?> UpdateStatusAsync(
        string workspaceRoot, string issueId, string status, CancellationToken cancellationToken = default)
    {
        var file = IssueFilePath(workspaceRoot, issueId);
        if (!File.Exists(file))
            return null;

        IssueFileModel? model;
        try
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            model = JsonSerializer.Deserialize<IssueFileModel>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }

        if (model is null)
            return null;

        var updated = model with { Status = status };
        var updatedJson = JsonSerializer.Serialize(updated, JsonOptions);
        await File.WriteAllTextAsync(file, updatedJson, cancellationToken);

        return ToDto(updated, issueId);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string GenerateNextId(string issuesDir)
    {
        var max = 0;
        foreach (var file in Directory.EnumerateFiles(issuesDir, "ISS-*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file); // e.g. "ISS-0001"
            if (name.Length > 4 && int.TryParse(name[4..], out var num))
                max = Math.Max(max, num);
        }
        return $"ISS-{max + 1:D4}";
    }

    private static async Task<IssueDto?> ReadIssueFileAsync(string path, CancellationToken cancellationToken)
    {
        IssueFileModel? model;
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            model = JsonSerializer.Deserialize<IssueFileModel>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }

        if (model is null)
            return null;

        var fallbackId = Path.GetFileNameWithoutExtension(path);
        return ToDto(model, fallbackId);
    }

    private static IssueDto ToDto(IssueFileModel model, string fallbackId) => new()
    {
        Id = model.Id ?? fallbackId,
        Title = model.Title ?? string.Empty,
        Status = model.Status ?? "open",
        Severity = model.Severity,
        Scope = model.Scope,
        Repro = model.Repro,
        Expected = model.Expected,
        Actual = model.Actual,
        ImpactedFiles = model.ImpactedFiles ?? [],
        PhaseId = model.PhaseId ?? model.Phase,
        TaskId = model.TaskId ?? model.Task,
        MilestoneId = model.MilestoneId ?? model.Milestone,
    };

    // ── On-disk JSON deserialization model ────────────────────────────────────
    // Mirrors the AOS issue.schema.json. Nullable members tolerate missing fields.
    // Alternate field names handle common variants written by the agent side.

    private sealed record IssueFileModel
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Status { get; init; }
        public string? Severity { get; init; }
        public string? Scope { get; init; }
        public string? Repro { get; init; }
        public string? Expected { get; init; }
        public string? Actual { get; init; }
        public List<string>? ImpactedFiles { get; init; }
        // Phase reference — agent files may use "phaseId" or "phase"
        public string? PhaseId { get; init; }
        public string? Phase { get; init; }
        // Task reference — agent files may use "taskId" or "task"
        [JsonPropertyName("taskId")]
        public string? TaskId { get; init; }
        public string? Task { get; init; }
        // Milestone reference — agent files may use "milestoneId" or "milestone"
        public string? MilestoneId { get; init; }
        public string? Milestone { get; init; }
    }
}

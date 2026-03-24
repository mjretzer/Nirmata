using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Data.Dto.Models.OrchestratorGate;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Derives the orchestrator gate and timeline by reading workspace spec, state, evidence,
/// and UAT artifacts from disk. All file reads are resilient to missing or malformed files —
/// absent data is surfaced as a failing check rather than a hard error.
/// </summary>
public sealed class OrchestratorGateService : IOrchestratorGateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Public API ──────────────────────────────────────────────────────────

    public async Task<OrchestratorGateDto> GetGateAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var checks = new List<OrchestratorGateCheckDto>();

        // ── 1. Workspace prerequisite checks ─────────────────────────────────

        var projectPath = Path.Combine(workspaceRoot, ".aos", "spec", "project.json");
        var projectExists = File.Exists(projectPath);
        checks.Add(MakeCheck(
            "workspace.project", "workspace", "Project spec exists",
            projectExists
                ? "project.json is present"
                : "project.json is missing — run new-project to create it",
            projectExists ? GateCheckStatus.Pass : GateCheckStatus.Fail));

        if (!projectExists)
            return Build(null, null, checks, "new-project");

        var roadmapPath = Path.Combine(workspaceRoot, ".aos", "spec", "roadmap.json");
        var roadmapExists = File.Exists(roadmapPath);
        checks.Add(MakeCheck(
            "workspace.roadmap", "workspace", "Roadmap exists",
            roadmapExists
                ? "roadmap.json is present"
                : "roadmap.json is missing — run create-roadmap",
            roadmapExists ? GateCheckStatus.Pass : GateCheckStatus.Fail));

        if (!roadmapExists)
            return Build(null, null, checks, "create-roadmap");

        // ── 2. Load state cursor ──────────────────────────────────────────────

        var state = await LoadStateAsync(workspaceRoot, cancellationToken);
        var cursorTaskId = state?.Position?.TaskId;
        var cursorPhaseId = state?.Position?.PhaseId;

        // ── 3. Find current / next task ───────────────────────────────────────

        var tasks = await LoadTasksAsync(workspaceRoot, cancellationToken);

        // Prefer the task identified by the state cursor; fall back to the first non-Done task.
        TaskRecord? task = cursorTaskId is not null
            ? tasks.FirstOrDefault(t => t.Id.Equals(cursorTaskId, StringComparison.OrdinalIgnoreCase))
            : null;

        task ??= tasks.FirstOrDefault(
            t => !(t.Status ?? string.Empty).Equals("Done", StringComparison.OrdinalIgnoreCase));

        if (task is null)
        {
            // No pending tasks — workspace either needs new plans or is fully complete.
            var noTaskDetail = tasks.Count == 0
                ? "No tasks found in spec/tasks/ — run plan-phase to generate task plans"
                : "All known tasks are marked Done";

            checks.Add(MakeCheck(
                "workspace.tasks", "workspace", "Tasks available",
                noTaskDetail, GateCheckStatus.Warn));

            var action = cursorPhaseId is not null
                ? $"plan-phase {cursorPhaseId}"
                : "plan-phase";

            return Build(null, null, checks, action);
        }

        // ── 4. Task plan check ────────────────────────────────────────────────

        var planPath = Path.Combine(workspaceRoot, ".aos", "spec", "tasks", task.Id, "plan.json");
        var planExists = File.Exists(planPath);
        checks.Add(MakeCheck(
            "plan.exists", "dependency", "Task plan exists",
            planExists
                ? $"{task.Id}/plan.json is present"
                : $"{task.Id}/plan.json is missing — run plan-phase",
            planExists ? GateCheckStatus.Pass : GateCheckStatus.Fail));

        if (!planExists)
        {
            var phaseHint = task.PhaseId is not null ? $" {task.PhaseId}" : string.Empty;
            return Build(task.Id, task.Title, checks, $"plan-phase{phaseHint}");
        }

        // ── 5. Blocker check ──────────────────────────────────────────────────

        var activeBlockers = (state?.Blockers ?? [])
            .Where(b =>
                b.AffectedTask is null ||
                b.AffectedTask.Equals(task.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (activeBlockers.Count > 0)
        {
            var firstDescription = activeBlockers[0].Description ?? "unspecified blocker";
            checks.Add(MakeCheck(
                "dependency.blockers", "dependency", "No active blockers",
                $"Blocker: {firstDescription}", GateCheckStatus.Fail));
            return Build(task.Id, task.Title, checks, "Resolve the open blocker before continuing");
        }

        checks.Add(MakeCheck(
            "dependency.blockers", "dependency", "No active blockers",
            "No open blockers for this task", GateCheckStatus.Pass));

        // ── 6. Evidence check ─────────────────────────────────────────────────

        var latestEvidencePath = Path.Combine(
            workspaceRoot, ".aos", "evidence", "task-evidence", task.Id, "latest.json");

        string evidenceCheckStatus;
        string evidenceDetail;

        if (File.Exists(latestEvidencePath))
        {
            var ev = await TryDeserializeAsync<EvidenceLatestModel>(latestEvidencePath, cancellationToken);
            var passed = ev?.Status?.Equals("pass", StringComparison.OrdinalIgnoreCase) == true;
            evidenceCheckStatus = passed ? GateCheckStatus.Pass : GateCheckStatus.Warn;
            evidenceDetail = passed
                ? $"Execution evidence for {task.Id} is present and passed"
                : $"Execution evidence for {task.Id} exists but reported status '{ev?.Status ?? "unknown"}'";
        }
        else
        {
            evidenceCheckStatus = GateCheckStatus.Fail;
            evidenceDetail = $"No execution evidence found for {task.Id} — run execute-plan";
        }

        checks.Add(MakeCheck(
            "evidence.run", "evidence", "Execution evidence exists",
            evidenceDetail, evidenceCheckStatus));

        if (evidenceCheckStatus == GateCheckStatus.Fail)
            return Build(task.Id, task.Title, checks, "execute-plan");

        // ── 7. UAT check ──────────────────────────────────────────────────────

        var uatPath = Path.Combine(workspaceRoot, ".aos", "spec", "tasks", task.Id, "uat.json");

        string uatCheckStatus;
        string uatDetail;

        if (File.Exists(uatPath))
        {
            var uat = await TryDeserializeAsync<UatStatusModel>(uatPath, cancellationToken);
            var uatResult = uat?.Status ?? "unknown";
            var passed = uatResult.Equals("passed", StringComparison.OrdinalIgnoreCase);
            var failed = uatResult.Equals("failed", StringComparison.OrdinalIgnoreCase);

            uatCheckStatus = passed ? GateCheckStatus.Pass
                : failed ? GateCheckStatus.Fail
                : GateCheckStatus.Warn;
            uatDetail = $"UAT for {task.Id}: {uatResult}";
        }
        else
        {
            uatCheckStatus = GateCheckStatus.Fail;
            uatDetail = $"No UAT record for {task.Id} — run verify-work";
        }

        checks.Add(MakeCheck(
            "uat.status", "uat", "UAT verification",
            uatDetail, uatCheckStatus));

        if (uatCheckStatus == GateCheckStatus.Fail)
        {
            // Distinguish "needs verification" from "verification failed".
            var action = uatPath is not null && File.Exists(uatPath)
                ? "plan-fix"
                : (task.PhaseId is not null ? $"verify-work {task.PhaseId}" : "verify-work");
            return Build(task.Id, task.Title, checks, action);
        }

        if (uatCheckStatus == GateCheckStatus.Warn)
        {
            var phaseHint = task.PhaseId is not null ? $" {task.PhaseId}" : string.Empty;
            return Build(task.Id, task.Title, checks, $"verify-work{phaseHint}");
        }

        // All checks passed — workspace is ready to advance.
        return Build(task.Id, task.Title, checks, null);
    }

    public async Task<OrchestratorTimelineDto> GetTimelineAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var state = await LoadStateAsync(workspaceRoot, cancellationToken);
        var currentPhaseId = state?.Position?.PhaseId;

        var phasesDir = Path.Combine(workspaceRoot, ".aos", "spec", "phases");
        if (!Directory.Exists(phasesDir))
            return new OrchestratorTimelineDto { Steps = [] };

        var steps = new List<OrchestratorTimelineStepDto>();

        foreach (var dir in Directory.EnumerateDirectories(phasesDir).Order())
        {
            var phaseFile = Path.Combine(dir, "phase.json");
            var dirName = Path.GetFileName(dir);

            PhaseModel? phase = null;
            if (File.Exists(phaseFile))
                phase = await TryDeserializeAsync<PhaseModel>(phaseFile, cancellationToken);

            var phaseId = phase?.Id ?? dirName;
            var label = phase?.Title ?? phaseId;
            var fileStatus = phase?.Status;

            string stepStatus;
            if (fileStatus?.Equals("Done", StringComparison.OrdinalIgnoreCase) == true ||
                fileStatus?.Equals("Completed", StringComparison.OrdinalIgnoreCase) == true)
            {
                stepStatus = "completed";
            }
            else if (phaseId.Equals(currentPhaseId, StringComparison.OrdinalIgnoreCase))
            {
                stepStatus = "active";
            }
            else if (currentPhaseId is not null &&
                     string.Compare(phaseId, currentPhaseId, StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Phases that alphabetically precede the cursor phase are considered completed.
                stepStatus = "completed";
            }
            else
            {
                stepStatus = "pending";
            }

            steps.Add(new OrchestratorTimelineStepDto
            {
                Id = phaseId,
                Label = label,
                Status = stepStatus,
            });
        }

        return new OrchestratorTimelineDto { Steps = steps };
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static OrchestratorGateCheckDto MakeCheck(
        string id, string kind, string label, string detail, string status) =>
        new() { Id = id, Kind = kind, Label = label, Detail = detail, Status = status };

    /// <summary>
    /// Builds the gate DTO. <paramref name="recommendedAction"/> being <see langword="null"/>
    /// means all checks have passed and the workspace is in a clean state.
    /// <c>Runnable</c> is <see langword="true"/> when no check has a <see cref="GateCheckStatus.Fail"/>
    /// status.
    /// </summary>
    private static OrchestratorGateDto Build(
        string? taskId,
        string? taskTitle,
        IReadOnlyList<OrchestratorGateCheckDto> checks,
        string? recommendedAction) =>
        new()
        {
            TaskId = taskId,
            TaskTitle = taskTitle,
            Runnable = !checks.Any(c => c.Status == GateCheckStatus.Fail),
            RecommendedAction = recommendedAction,
            Checks = checks,
        };

    private async Task<WorkspaceStateModel?> LoadStateAsync(
        string workspaceRoot, CancellationToken cancellationToken)
    {
        var statePath = Path.Combine(workspaceRoot, ".aos", "state", "state.json");
        return await TryDeserializeAsync<WorkspaceStateModel>(statePath, cancellationToken);
    }

    private async Task<IReadOnlyList<TaskRecord>> LoadTasksAsync(
        string workspaceRoot, CancellationToken cancellationToken)
    {
        var tasksDir = Path.Combine(workspaceRoot, ".aos", "spec", "tasks");
        if (!Directory.Exists(tasksDir))
            return [];

        var results = new List<TaskRecord>();

        foreach (var dir in Directory.EnumerateDirectories(tasksDir).Order())
        {
            var taskFile = Path.Combine(dir, "task.json");
            if (!File.Exists(taskFile))
                continue;

            var model = await TryDeserializeAsync<TaskModel>(taskFile, cancellationToken);
            var id = model?.Id ?? Path.GetFileName(dir);

            results.Add(new TaskRecord(
                Id: id,
                PhaseId: model?.PhaseId ?? model?.Phase,
                Title: model?.Title,
                Status: model?.Status ?? string.Empty));
        }

        return results;
    }

    private static async Task<T?> TryDeserializeAsync<T>(
        string path, CancellationToken cancellationToken) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    // ── Private JSON deserialization models ───────────────────────────────────
    // Mirror the AOS artifact schemas (documents/architecture/schemas.md).
    // Nullable members handle missing or renamed fields gracefully.

    private sealed class WorkspaceStateModel
    {
        public PositionModel? Position { get; init; }
        public IReadOnlyList<BlockerModel>? Blockers { get; init; }
    }

    private sealed class PositionModel
    {
        public string? TaskId { get; init; }
        public string? PhaseId { get; init; }
        public string? MilestoneId { get; init; }
        public string? Status { get; init; }
    }

    private sealed class BlockerModel
    {
        public string? Id { get; init; }
        public string? Description { get; init; }
        [JsonPropertyName("affectedTask")]
        public string? AffectedTask { get; init; }
    }

    private sealed class TaskModel
    {
        public string? Id { get; init; }
        public string? PhaseId { get; init; }
        public string? Phase { get; init; }
        public string? Title { get; init; }
        public string? Status { get; init; }
    }

    private sealed class EvidenceLatestModel
    {
        public string? Status { get; init; }
    }

    private sealed class UatStatusModel
    {
        public string? Status { get; init; }
    }

    private sealed class PhaseModel
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Status { get; init; }
    }

    private sealed record TaskRecord(string Id, string? PhaseId, string? Title, string? Status);
}

using System.Text.Json;
using nirmata.Data.Dto.Models.OrchestratorGate;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Tests for <see cref="OrchestratorGateService"/>.
/// Each test creates an isolated temp workspace and tears it down afterwards.
/// </summary>
public sealed class OrchestratorGateServiceTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly OrchestratorGateService _sut = new();

    public OrchestratorGateServiceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "nirm-gate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
            Directory.Delete(_workspaceRoot, recursive: true);
    }

    // ── Gate derivation — prerequisite gating ───────────────────────────────

    [Fact]
    public async Task GetGateAsync_MissingProjectJson_IsNotRunnableAndRecommendsNewProject()
    {
        // Arrange: empty workspace — no .aos/spec/project.json.

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.False(gate.Runnable);
        Assert.Equal("new-project", gate.RecommendedAction);
    }

    [Fact]
    public async Task GetGateAsync_MissingRoadmapJson_IsNotRunnableAndRecommendsCreateRoadmap()
    {
        // Arrange: project.json exists but no roadmap.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.False(gate.Runnable);
        Assert.Equal("create-roadmap", gate.RecommendedAction);
    }

    [Fact]
    public async Task GetGateAsync_NoTasks_RecommendsPlanPhaseWithWarnCheck()
    {
        // Arrange: both spec files exist but no tasks directory.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert: no Fail checks → Runnable is true; but a Warn is raised and action is plan-phase.
        Assert.Contains("plan-phase", gate.RecommendedAction);
        var warnCheck = Assert.Single(gate.Checks, c => c.Status == GateCheckStatus.Warn);
        Assert.Equal("workspace.tasks", warnCheck.Id);
    }

    [Fact]
    public async Task GetGateAsync_TaskPlanMissing_IsNotRunnableAndRecommendsPlanPhase()
    {
        // Arrange: task.json exists but no plan.json.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "First task",
            phaseId = "PH-0001",
            status = "Planned",
        });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.False(gate.Runnable);
        Assert.Contains("plan-phase", gate.RecommendedAction);
    }

    [Fact]
    public async Task GetGateAsync_ActiveBlocker_IsNotRunnableAndMentionsBlocker()
    {
        // Arrange: plan exists but a blocker is in state.json.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "First task",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteTaskPlanAsync("TSK-000001", new { taskId = "TSK-000001" });
        await WriteStateFileAsync(new
        {
            position = new { taskId = "TSK-000001", phaseId = "PH-0001", status = "InProgress" },
            blockers = new[]
            {
                new { id = "BLK-001", description = "Waiting for API key", affectedTask = "TSK-000001" },
            },
        });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.False(gate.Runnable);
        var blockerCheck = Assert.Single(gate.Checks, c => c.Kind == "dependency" && c.Id == "dependency.blockers");
        Assert.Equal(GateCheckStatus.Fail, blockerCheck.Status);
    }

    [Fact]
    public async Task GetGateAsync_EvidenceMissing_IsNotRunnableAndRecommendsExecutePlan()
    {
        // Arrange: plan and no blockers, but no evidence.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "First task",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteTaskPlanAsync("TSK-000001", new { taskId = "TSK-000001" });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.False(gate.Runnable);
        Assert.Equal("execute-plan", gate.RecommendedAction);
    }

    [Fact]
    public async Task GetGateAsync_EvidencePassNoUat_IsNotRunnableAndRecommendsVerifyWork()
    {
        // Arrange: evidence pass, but no UAT record yet.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "First task",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteTaskPlanAsync("TSK-000001", new { taskId = "TSK-000001" });
        await WriteEvidenceAsync("TSK-000001", new { status = "pass" });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.False(gate.Runnable);
        Assert.Contains("verify-work", gate.RecommendedAction);
    }

    [Fact]
    public async Task GetGateAsync_UatFailed_IsNotRunnableAndRecommendsPlanFix()
    {
        // Arrange: evidence pass + UAT failed.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "First task",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteTaskPlanAsync("TSK-000001", new { taskId = "TSK-000001" });
        await WriteEvidenceAsync("TSK-000001", new { status = "pass" });
        await WriteUatAsync("TSK-000001", new { status = "failed" });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.False(gate.Runnable);
        Assert.Equal("plan-fix", gate.RecommendedAction);
    }

    [Fact]
    public async Task GetGateAsync_AllChecksPassed_IsRunnable()
    {
        // Arrange: project + roadmap + task + plan + pass evidence + pass UAT.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "First task",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteTaskPlanAsync("TSK-000001", new { taskId = "TSK-000001" });
        await WriteEvidenceAsync("TSK-000001", new { status = "pass" });
        await WriteUatAsync("TSK-000001", new { status = "passed" });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.True(gate.Runnable);
        Assert.Null(gate.RecommendedAction);
    }

    // ── Gate check content ───────────────────────────────────────────────────

    [Fact]
    public async Task GetGateAsync_Checks_AllHaveRequiredFields()
    {
        // Arrange: minimal setup — just project to get some checks.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert: every check carries all required contract fields.
        Assert.All(gate.Checks, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Id));
            Assert.False(string.IsNullOrWhiteSpace(c.Kind));
            Assert.False(string.IsNullOrWhiteSpace(c.Label));
            Assert.False(string.IsNullOrWhiteSpace(c.Detail));
            Assert.Contains(c.Status, new[] { GateCheckStatus.Pass, GateCheckStatus.Fail, GateCheckStatus.Warn });
        });
    }

    [Fact]
    public async Task GetGateAsync_TaskId_IsPopulatedWhenTaskExists()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "Implement service",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteTaskPlanAsync("TSK-000001", new { taskId = "TSK-000001" });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.Equal("TSK-000001", gate.TaskId);
    }

    [Fact]
    public async Task GetGateAsync_RecommendedAction_IsNullWhenAllPass()
    {
        // Arrange: fully passing workspace.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "First task",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteTaskPlanAsync("TSK-000001", new { taskId = "TSK-000001" });
        await WriteEvidenceAsync("TSK-000001", new { status = "pass" });
        await WriteUatAsync("TSK-000001", new { status = "passed" });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert
        Assert.Null(gate.RecommendedAction);
    }

    [Fact]
    public async Task GetGateAsync_StateCursorPicksCorrectTask()
    {
        // Arrange: two tasks; cursor points to TSK-000002.
        await WriteSpecFileAsync("project.json", new { name = "Test" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        await WriteTaskFileAsync("TSK-000001", new
        {
            id = "TSK-000001",
            title = "First task",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteTaskFileAsync("TSK-000002", new
        {
            id = "TSK-000002",
            title = "Second task",
            phaseId = "PH-0001",
            status = "Planned",
        });
        await WriteStateFileAsync(new
        {
            position = new { taskId = "TSK-000002", phaseId = "PH-0001", status = "InProgress" },
            blockers = Array.Empty<object>(),
        });

        // Act
        var gate = await _sut.GetGateAsync(_workspaceRoot);

        // Assert: cursor task selected even though TSK-000001 alphabetically precedes it.
        Assert.Equal("TSK-000002", gate.TaskId);
    }

    // ── GetTimelineAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTimelineAsync_NoPhasesDirectory_ReturnsEmptySteps()
    {
        // Arrange: no .aos/spec/phases/ directory.

        // Act
        var timeline = await _sut.GetTimelineAsync(_workspaceRoot);

        // Assert
        Assert.NotNull(timeline);
        Assert.Empty(timeline.Steps);
    }

    [Fact]
    public async Task GetTimelineAsync_WithPhases_ReturnsStepsInOrder()
    {
        // Arrange: three phases.
        await WritePhaseFileAsync("PH-0001", new { id = "PH-0001", title = "Foundation", status = "Done" });
        await WritePhaseFileAsync("PH-0002", new { id = "PH-0002", title = "Core", status = "InProgress" });
        await WritePhaseFileAsync("PH-0003", new { id = "PH-0003", title = "Polish", status = "Planned" });

        // Act
        var timeline = await _sut.GetTimelineAsync(_workspaceRoot);

        // Assert: steps are ordered by directory name.
        Assert.Equal(3, timeline.Steps.Count);
        Assert.Equal("PH-0001", timeline.Steps[0].Id);
        Assert.Equal("PH-0002", timeline.Steps[1].Id);
        Assert.Equal("PH-0003", timeline.Steps[2].Id);
    }

    [Fact]
    public async Task GetTimelineAsync_DonePhase_HasCompletedStatus()
    {
        // Arrange
        await WritePhaseFileAsync("PH-0001", new { id = "PH-0001", title = "Foundation", status = "Done" });

        // Act
        var timeline = await _sut.GetTimelineAsync(_workspaceRoot);

        // Assert
        Assert.Single(timeline.Steps);
        Assert.Equal("completed", timeline.Steps[0].Status);
    }

    [Fact]
    public async Task GetTimelineAsync_CurrentCursorPhase_HasActiveStatus()
    {
        // Arrange: state.json points cursor at PH-0002.
        await WritePhaseFileAsync("PH-0001", new { id = "PH-0001", title = "Foundation", status = "Done" });
        await WritePhaseFileAsync("PH-0002", new { id = "PH-0002", title = "Core", status = "InProgress" });
        await WritePhaseFileAsync("PH-0003", new { id = "PH-0003", title = "Polish", status = "Planned" });
        await WriteStateFileAsync(new
        {
            position = new { phaseId = "PH-0002", status = "InProgress" },
            blockers = Array.Empty<object>(),
        });

        // Act
        var timeline = await _sut.GetTimelineAsync(_workspaceRoot);

        // Assert
        var active = Assert.Single(timeline.Steps, s => s.Status == "active");
        Assert.Equal("PH-0002", active.Id);
    }

    [Fact]
    public async Task GetTimelineAsync_PhasesBeforeCursor_AreCompleted()
    {
        // Arrange: PH-0001 is alphabetically before PH-0002 (active cursor).
        await WritePhaseFileAsync("PH-0001", new { id = "PH-0001", title = "Foundation", status = "Planned" });
        await WritePhaseFileAsync("PH-0002", new { id = "PH-0002", title = "Core", status = "InProgress" });
        await WriteStateFileAsync(new
        {
            position = new { phaseId = "PH-0002", status = "InProgress" },
            blockers = Array.Empty<object>(),
        });

        // Act
        var timeline = await _sut.GetTimelineAsync(_workspaceRoot);

        // Assert: PH-0001 should be completed because it is before the cursor.
        var ph1 = Assert.Single(timeline.Steps, s => s.Id == "PH-0001");
        Assert.Equal("completed", ph1.Status);
    }

    [Fact]
    public async Task GetTimelineAsync_PhasesAfterCursor_ArePending()
    {
        // Arrange: PH-0003 is after cursor at PH-0002.
        await WritePhaseFileAsync("PH-0002", new { id = "PH-0002", title = "Core", status = "InProgress" });
        await WritePhaseFileAsync("PH-0003", new { id = "PH-0003", title = "Polish", status = "Planned" });
        await WriteStateFileAsync(new
        {
            position = new { phaseId = "PH-0002", status = "InProgress" },
            blockers = Array.Empty<object>(),
        });

        // Act
        var timeline = await _sut.GetTimelineAsync(_workspaceRoot);

        // Assert
        var ph3 = Assert.Single(timeline.Steps, s => s.Id == "PH-0003");
        Assert.Equal("pending", ph3.Status);
    }

    [Fact]
    public async Task GetTimelineAsync_EachStep_HasStableFields()
    {
        // Arrange
        await WritePhaseFileAsync("PH-0001", new { id = "PH-0001", title = "Foundation" });

        // Act
        var timeline = await _sut.GetTimelineAsync(_workspaceRoot);

        // Assert
        Assert.All(timeline.Steps, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.False(string.IsNullOrWhiteSpace(s.Label));
            Assert.False(string.IsNullOrWhiteSpace(s.Status));
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string SpecDir => Path.Combine(_workspaceRoot, ".aos", "spec");
    private string StateDir => Path.Combine(_workspaceRoot, ".aos", "state");

    private async Task WriteSpecFileAsync(string fileName, object obj)
    {
        Directory.CreateDirectory(SpecDir);
        await File.WriteAllTextAsync(
            Path.Combine(SpecDir, fileName),
            JsonSerializer.Serialize(obj));
    }

    private async Task WriteStateFileAsync(object obj)
    {
        Directory.CreateDirectory(StateDir);
        await File.WriteAllTextAsync(
            Path.Combine(StateDir, "state.json"),
            JsonSerializer.Serialize(obj));
    }

    private async Task WriteTaskFileAsync(string taskId, object taskObj)
    {
        var taskDir = Path.Combine(SpecDir, "tasks", taskId);
        Directory.CreateDirectory(taskDir);
        await File.WriteAllTextAsync(
            Path.Combine(taskDir, "task.json"),
            JsonSerializer.Serialize(taskObj));
    }

    private async Task WriteTaskPlanAsync(string taskId, object planObj)
    {
        var taskDir = Path.Combine(SpecDir, "tasks", taskId);
        Directory.CreateDirectory(taskDir);
        await File.WriteAllTextAsync(
            Path.Combine(taskDir, "plan.json"),
            JsonSerializer.Serialize(planObj));
    }

    private async Task WriteEvidenceAsync(string taskId, object evidenceObj)
    {
        var evidenceDir = Path.Combine(_workspaceRoot, ".aos", "evidence", "task-evidence", taskId);
        Directory.CreateDirectory(evidenceDir);
        await File.WriteAllTextAsync(
            Path.Combine(evidenceDir, "latest.json"),
            JsonSerializer.Serialize(evidenceObj));
    }

    private async Task WriteUatAsync(string taskId, object uatObj)
    {
        var taskDir = Path.Combine(SpecDir, "tasks", taskId);
        Directory.CreateDirectory(taskDir);
        await File.WriteAllTextAsync(
            Path.Combine(taskDir, "uat.json"),
            JsonSerializer.Serialize(uatObj));
    }

    private async Task WritePhaseFileAsync(string phaseId, object phaseObj)
    {
        var phaseDir = Path.Combine(SpecDir, "phases", phaseId);
        Directory.CreateDirectory(phaseDir);
        await File.WriteAllTextAsync(
            Path.Combine(phaseDir, "phase.json"),
            JsonSerializer.Serialize(phaseObj));
    }
}

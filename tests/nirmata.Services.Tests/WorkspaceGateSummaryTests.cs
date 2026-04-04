using System.Security.Cryptography;
using System.Text.Json;
using nirmata.Data.Dto.Models.WorkspaceStatus;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Tests for <see cref="OrchestratorGateService.GetGateSummaryAsync"/>.
/// Covers gate transitions as canonical artifacts are added to the workspace
/// and brownfield codebase readiness states (missing / stale).
/// Each test creates an isolated temp workspace and tears it down afterwards.
/// </summary>
public sealed class WorkspaceGateSummaryTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly OrchestratorGateService _sut = new();

    public WorkspaceGateSummaryTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(), "nirm-summary-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
            Directory.Delete(_workspaceRoot, recursive: true);
    }

    // ── Gate progression: interview → roadmap → planning ────────────────────

    [Fact]
    public async Task GetGateSummaryAsync_EmptyWorkspace_CurrentGateIsInterview()
    {
        // Arrange: no artifacts at all.

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.Equal(WorkspaceGate.Interview, summary.CurrentGate);
    }

    [Fact]
    public async Task GetGateSummaryAsync_EmptyWorkspace_NextStepIsNewProject()
    {
        // Arrange: no artifacts.

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.Equal("new-project", summary.NextRequiredStep);
    }

    [Fact]
    public async Task GetGateSummaryAsync_EmptyWorkspace_BlockingReasonMentionsProjectJson()
    {
        // Arrange: no artifacts.

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: blocking reason comes from the failing workspace check.
        Assert.NotNull(summary.BlockingReason);
        Assert.Contains("project.json", summary.BlockingReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetGateSummaryAsync_AfterAddingProjectJson_CurrentGateIsRoadmap()
    {
        // Arrange: project spec exists, roadmap does not.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: gate advanced from interview to roadmap.
        Assert.Equal(WorkspaceGate.Roadmap, summary.CurrentGate);
    }

    [Fact]
    public async Task GetGateSummaryAsync_AfterAddingProjectJson_NextStepIsCreateRoadmap()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.Equal("create-roadmap", summary.NextRequiredStep);
    }

    [Fact]
    public async Task GetGateSummaryAsync_AfterAddingRoadmapJson_CurrentGateIsPlanning()
    {
        // Arrange: project and roadmap exist; no tasks and no codebase directory.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: gate advanced from roadmap to planning.
        Assert.Equal(WorkspaceGate.Planning, summary.CurrentGate);
    }

    [Fact]
    public async Task GetGateSummaryAsync_AfterAddingRoadmapJson_NextStepContainsPlanPhase()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.NotNull(summary.NextRequiredStep);
        Assert.Contains("plan-phase", summary.NextRequiredStep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetGateSummaryAsync_ArtifactProgression_GateTransitionsCorrectly()
    {
        // Arrange / Act / Assert — step by step through the interview → roadmap → planning chain.

        // Step 1: empty workspace → interview.
        var s1 = await _sut.GetGateSummaryAsync(_workspaceRoot);
        Assert.Equal(WorkspaceGate.Interview, s1.CurrentGate);

        // Step 2: add project.json → roadmap.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        var s2 = await _sut.GetGateSummaryAsync(_workspaceRoot);
        Assert.Equal(WorkspaceGate.Roadmap, s2.CurrentGate);

        // Step 3: add roadmap.json → planning (no codebase directory → greenfield, no preflight).
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        var s3 = await _sut.GetGateSummaryAsync(_workspaceRoot);
        Assert.Equal(WorkspaceGate.Planning, s3.CurrentGate);
    }

    // ── Brownfield preflight: map missing ───────────────────────────────────

    [Fact]
    public async Task GetGateSummaryAsync_CodebaseDirExistsNoMap_CurrentGateIsCodebasePreflight()
    {
        // Arrange: project.json present, roadmap absent, codebase directory exists but map.json missing.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        CreateCodebaseDir(); // creates .aos/codebase/ without map.json

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: missing map upgrades roadmap gate to codebase-preflight.
        Assert.Equal(WorkspaceGate.CodebasePreflight, summary.CurrentGate);
    }

    [Fact]
    public async Task GetGateSummaryAsync_CodebaseDirExistsNoMap_CodebaseReadinessMapStatusIsMissing()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        CreateCodebaseDir();

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.NotNull(summary.CodebaseReadiness);
        Assert.Equal("missing", summary.CodebaseReadiness.MapStatus);
    }

    [Fact]
    public async Task GetGateSummaryAsync_CodebaseDirExistsNoMap_CodebaseReadinessLastUpdatedIsNull()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        CreateCodebaseDir();

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: no file → no timestamp.
        Assert.NotNull(summary.CodebaseReadiness);
        Assert.Null(summary.CodebaseReadiness.LastUpdated);
    }

    [Fact]
    public async Task GetGateSummaryAsync_CodebaseDirExistsNoMap_NextStepIsMapCodebase()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        CreateCodebaseDir();

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.Equal("map-codebase", summary.NextRequiredStep);
    }

    [Fact]
    public async Task GetGateSummaryAsync_CodebaseDirExistsNoMap_BlockingReasonPresent()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        CreateCodebaseDir();

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: blocking reason comes from the codebase readiness detail.
        Assert.NotNull(summary.BlockingReason);
        Assert.NotEmpty(summary.BlockingReason);
    }

    // ── Brownfield preflight: map stale ──────────────────────────────────────

    [Fact]
    public async Task GetGateSummaryAsync_MapExistsNoManifest_CurrentGateIsCodebasePreflight()
    {
        // Arrange: project.json present, roadmap absent, map.json exists but no hash-manifest.json.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteCodebaseMapAsync(new { generated = "2026-01-01" });
        // No hash-manifest.json → service cannot verify hash → map is stale.

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.Equal(WorkspaceGate.CodebasePreflight, summary.CurrentGate);
    }

    [Fact]
    public async Task GetGateSummaryAsync_MapExistsNoManifest_CodebaseReadinessMapStatusIsStale()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteCodebaseMapAsync(new { generated = "2026-01-01" });

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.NotNull(summary.CodebaseReadiness);
        Assert.Equal("stale", summary.CodebaseReadiness.MapStatus);
    }

    [Fact]
    public async Task GetGateSummaryAsync_MapExistsNoManifest_CodebaseReadinessLastUpdatedNotNull()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteCodebaseMapAsync(new { generated = "2026-01-01" });

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: file exists → timestamp must be populated.
        Assert.NotNull(summary.CodebaseReadiness);
        Assert.NotNull(summary.CodebaseReadiness.LastUpdated);
    }

    [Fact]
    public async Task GetGateSummaryAsync_MapExistsNoManifest_NextStepIsMapCodebase()
    {
        // Arrange
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteCodebaseMapAsync(new { generated = "2026-01-01" });

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.Equal("map-codebase", summary.NextRequiredStep);
    }

    [Fact]
    public async Task GetGateSummaryAsync_MapExistsHashMismatch_CodebaseReadinessMapStatusIsStale()
    {
        // Arrange: map.json exists and a manifest exists but with a wrong hash.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteCodebaseMapAsync(new { generated = "2026-01-01" });
        await WriteHashManifestAsync("map.json", "0000000000000000000000000000000000000000000000000000000000000000");

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.NotNull(summary.CodebaseReadiness);
        Assert.Equal("stale", summary.CodebaseReadiness.MapStatus);
    }

    // ── Brownfield preflight: planning gate also affected ─────────────────────

    [Fact]
    public async Task GetGateSummaryAsync_RoadmapExistsMapMissing_CurrentGateIsCodebasePreflight()
    {
        // Arrange: roadmap exists (would normally yield planning) but codebase dir has no map.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        CreateCodebaseDir(); // no map.json

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: planning gate is also promoted to codebase-preflight when map is missing.
        Assert.Equal(WorkspaceGate.CodebasePreflight, summary.CurrentGate);
    }

    // ── Greenfield: no codebase directory should not trigger preflight ────────

    [Fact]
    public async Task GetGateSummaryAsync_NoCodbaseDir_PlanningGateNotPromotedToPreflight()
    {
        // Arrange: project.json + roadmap.json but no .aos/codebase/ directory at all.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        await WriteSpecFileAsync("roadmap.json", new { milestones = Array.Empty<object>() });
        // Deliberately do NOT create a codebase directory.

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: greenfield workspace stays at planning gate.
        Assert.Equal(WorkspaceGate.Planning, summary.CurrentGate);
        Assert.Null(summary.CodebaseReadiness);
    }

    // ── Current-hash manifest: ready map does not surface readiness details ───

    [Fact]
    public async Task GetGateSummaryAsync_MapHashCurrent_CodebaseReadinessIsNull()
    {
        // Arrange: map.json exists and hash-manifest.json carries the matching hash.
        await WriteSpecFileAsync("project.json", new { name = "Test Project" });
        var mapContent = await WriteCodebaseMapAsync(new { generated = "2026-01-01" });
        var mapPath = Path.Combine(_workspaceRoot, ".aos", "codebase", "map.json");
        var hash = await ComputeSha256HexAsync(mapPath);
        await WriteHashManifestAsync("map.json", hash);

        // Act
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert: ready map → no codebase readiness details, roadmap gate not promoted.
        Assert.Equal(WorkspaceGate.Roadmap, summary.CurrentGate);
        Assert.Null(summary.CodebaseReadiness);
    }

    // ── Ready workspace ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetGateSummaryAsync_AllChecksPassed_CurrentGateIsReady()
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
        var summary = await _sut.GetGateSummaryAsync(_workspaceRoot);

        // Assert
        Assert.Equal(WorkspaceGate.Ready, summary.CurrentGate);
        Assert.Null(summary.BlockingReason);
        Assert.Null(summary.NextRequiredStep);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string SpecDir     => Path.Combine(_workspaceRoot, ".aos", "spec");
    private string StateDir    => Path.Combine(_workspaceRoot, ".aos", "state");
    private string CodebaseDir => Path.Combine(_workspaceRoot, ".aos", "codebase");

    private async Task WriteSpecFileAsync(string fileName, object obj)
    {
        Directory.CreateDirectory(SpecDir);
        await File.WriteAllTextAsync(
            Path.Combine(SpecDir, fileName),
            JsonSerializer.Serialize(obj));
    }

    private void CreateCodebaseDir() =>
        Directory.CreateDirectory(CodebaseDir);

    /// <summary>Writes <c>.aos/codebase/map.json</c> and returns the written path.</summary>
    private async Task<string> WriteCodebaseMapAsync(object obj)
    {
        Directory.CreateDirectory(CodebaseDir);
        var path = Path.Combine(CodebaseDir, "map.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(obj));
        return path;
    }

    private async Task WriteHashManifestAsync(string artifactRelPath, string hexHash)
    {
        Directory.CreateDirectory(CodebaseDir);
        var manifest = new { files = new Dictionary<string, string> { [artifactRelPath] = hexHash } };
        await File.WriteAllTextAsync(
            Path.Combine(CodebaseDir, "hash-manifest.json"),
            JsonSerializer.Serialize(manifest));
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

    private static async Task<string> ComputeSha256HexAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

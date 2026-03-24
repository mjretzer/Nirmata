using System.Text.Json;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Unit tests for <see cref="UatService"/>.
/// Each test creates an isolated temp workspace and cleans up after itself.
/// </summary>
public sealed class UatServiceTests : IDisposable
{
    private readonly UatService _sut = new();
    private readonly string _root;

    public UatServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"nirmata-uat-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string UatDir() => Path.Combine(_root, ".aos", "spec", "uat");
    private string TaskDir(string taskId) => Path.Combine(_root, ".aos", "spec", "tasks", taskId);

    private void WriteUatFile(string path, object payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }

    // ── Empty workspace ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_EmptyWorkspace_ReturnsEmptySummary()
    {
        // Arrange — no .aos directory exists at all

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Records);
        Assert.Empty(result.TaskSummaries);
        Assert.Empty(result.PhaseSummaries);
    }

    [Fact]
    public async Task GetSummaryAsync_EmptyUatDirectory_ReturnsEmptySummary()
    {
        // Arrange — directory exists but contains no files
        Directory.CreateDirectory(UatDir());

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Empty(result.Records);
    }

    // ── Global UAT records (.aos/spec/uat/UAT-*.json) ─────────────────────────

    [Fact]
    public async Task GetSummaryAsync_GlobalUatRecords_AreLoaded()
    {
        // Arrange
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001",
            taskId = "TSK-000001",
            phaseId = "PH-0001",
            status = "passed",
        });
        WriteUatFile(Path.Combine(UatDir(), "UAT-0002.json"), new
        {
            id = "UAT-0002",
            taskId = "TSK-000002",
            phaseId = "PH-0001",
            status = "failed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Equal(2, result.Records.Count);
        Assert.Contains(result.Records, r => r.Id == "UAT-0001" && r.Status == "passed");
        Assert.Contains(result.Records, r => r.Id == "UAT-0002" && r.Status == "failed");
    }

    [Fact]
    public async Task GetSummaryAsync_GlobalUatRecord_FallsBackToFilenameWhenIdMissing()
    {
        // Arrange — no "id" field in the JSON
        WriteUatFile(Path.Combine(UatDir(), "UAT-0009.json"), new
        {
            status = "passed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert — fallback id = filename without extension
        Assert.Single(result.Records);
        Assert.Equal("UAT-0009", result.Records[0].Id);
    }

    // ── Task-level UAT records (.aos/spec/tasks/TSK-*/uat.json) ───────────────

    [Fact]
    public async Task GetSummaryAsync_TaskLevelUatRecords_AreLoaded()
    {
        // Arrange
        var taskDir = TaskDir("TSK-000001");
        WriteUatFile(Path.Combine(taskDir, "uat.json"), new
        {
            id = "UAT-T001",
            taskId = "TSK-000001",
            phaseId = "PH-0001",
            status = "passed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Single(result.Records);
        Assert.Equal("UAT-T001", result.Records[0].Id);
        Assert.Equal("TSK-000001", result.Records[0].TaskId);
    }

    [Fact]
    public async Task GetSummaryAsync_TaskLevelUatRecord_FallsBackToTaskDirNameWhenIdMissing()
    {
        // Arrange
        var taskDir = TaskDir("TSK-000007");
        WriteUatFile(Path.Combine(taskDir, "uat.json"), new
        {
            status = "failed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Single(result.Records);
        Assert.Equal("TSK-000007", result.Records[0].Id);
    }

    [Fact]
    public async Task GetSummaryAsync_TaskDirWithoutUatJson_IsSkipped()
    {
        // Arrange — task directory exists but has no uat.json
        Directory.CreateDirectory(TaskDir("TSK-000001"));
        File.WriteAllText(Path.Combine(TaskDir("TSK-000001"), "task.json"), "{}");

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Empty(result.Records);
    }

    // ── Task summary derivation ────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_AllPassedRecordsForTask_TaskSummaryIsPassed()
    {
        // Arrange
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", status = "passed",
        });
        WriteUatFile(Path.Combine(UatDir(), "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000001", status = "passed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        var taskSummary = Assert.Single(result.TaskSummaries);
        Assert.Equal("TSK-000001", taskSummary.TaskId);
        Assert.Equal("passed", taskSummary.Status);
        Assert.Equal(2, taskSummary.RecordCount);
    }

    [Fact]
    public async Task GetSummaryAsync_AnyFailedRecordForTask_TaskSummaryIsFailed()
    {
        // Arrange — one pass, one fail
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", status = "passed",
        });
        WriteUatFile(Path.Combine(UatDir(), "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000001", status = "failed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        var taskSummary = Assert.Single(result.TaskSummaries);
        Assert.Equal("failed", taskSummary.Status);
    }

    [Fact]
    public async Task GetSummaryAsync_UnknownStatusRecordsForTask_TaskSummaryIsUnknown()
    {
        // Arrange — status is neither "passed" nor "failed"
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", status = "in-progress",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        var taskSummary = Assert.Single(result.TaskSummaries);
        Assert.Equal("unknown", taskSummary.Status);
    }

    [Fact]
    public async Task GetSummaryAsync_RecordsWithNoTaskId_ExcludedFromTaskSummaries()
    {
        // Arrange — record has no taskId
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", status = "passed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert — record is present but does not generate a task summary
        Assert.Single(result.Records);
        Assert.Empty(result.TaskSummaries);
    }

    [Fact]
    public async Task GetSummaryAsync_MultipleTaskIds_ProducesOneSummaryPerTask()
    {
        // Arrange
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", status = "passed",
        });
        WriteUatFile(Path.Combine(UatDir(), "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", status = "failed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Equal(2, result.TaskSummaries.Count);
        Assert.Contains(result.TaskSummaries, s => s.TaskId == "TSK-000001" && s.Status == "passed");
        Assert.Contains(result.TaskSummaries, s => s.TaskId == "TSK-000002" && s.Status == "failed");
    }

    // ── Phase summary derivation ───────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_AllTasksInPhasePass_PhaseSummaryIsPassed()
    {
        // Arrange
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });
        WriteUatFile(Path.Combine(UatDir(), "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", phaseId = "PH-0001", status = "passed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        var phaseSummary = Assert.Single(result.PhaseSummaries);
        Assert.Equal("PH-0001", phaseSummary.PhaseId);
        Assert.Equal("passed", phaseSummary.Status);
        Assert.Contains("TSK-000001", phaseSummary.TaskIds);
        Assert.Contains("TSK-000002", phaseSummary.TaskIds);
    }

    [Fact]
    public async Task GetSummaryAsync_AnyTaskInPhaseFails_PhaseSummaryIsFailed()
    {
        // Arrange
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });
        WriteUatFile(Path.Combine(UatDir(), "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", phaseId = "PH-0001", status = "failed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        var phaseSummary = Assert.Single(result.PhaseSummaries);
        Assert.Equal("PH-0001", phaseSummary.PhaseId);
        Assert.Equal("failed", phaseSummary.Status);
    }

    [Fact]
    public async Task GetSummaryAsync_MultiplePhases_ProducesOneSummaryPerPhase()
    {
        // Arrange
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phaseId = "PH-0001", status = "passed",
        });
        WriteUatFile(Path.Combine(UatDir(), "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000002", phaseId = "PH-0002", status = "failed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Equal(2, result.PhaseSummaries.Count);
        Assert.Contains(result.PhaseSummaries, s => s.PhaseId == "PH-0001" && s.Status == "passed");
        Assert.Contains(result.PhaseSummaries, s => s.PhaseId == "PH-0002" && s.Status == "failed");
    }

    [Fact]
    public async Task GetSummaryAsync_RecordWithPhaseButNoTaskId_PhaseHasNoTasksAndIsUnknown()
    {
        // Arrange — phaseId present but taskId absent
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", phaseId = "PH-0001", status = "passed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert — phase summary exists but has no task references, status = unknown (no task summaries)
        var phaseSummary = Assert.Single(result.PhaseSummaries);
        Assert.Equal("PH-0001", phaseSummary.PhaseId);
        Assert.Empty(phaseSummary.TaskIds);
        Assert.Equal("unknown", phaseSummary.Status);
    }

    // ── Resilience ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_MalformedJsonFile_IsSkippedGracefully()
    {
        // Arrange — one valid file, one broken file
        var uatDir = UatDir();
        Directory.CreateDirectory(uatDir);
        File.WriteAllText(Path.Combine(uatDir, "UAT-0001.json"), "{ not valid json }}}");
        WriteUatFile(Path.Combine(uatDir, "UAT-0002.json"), new
        {
            id = "UAT-0002", taskId = "TSK-000001", status = "passed",
        });

        // Act — must not throw
        var result = await _sut.GetSummaryAsync(_root);

        // Assert — only the valid record is returned
        Assert.Single(result.Records);
        Assert.Equal("UAT-0002", result.Records[0].Id);
    }

    [Fact]
    public async Task GetSummaryAsync_EmptyJsonFile_IsSkippedGracefully()
    {
        // Arrange
        var uatDir = UatDir();
        Directory.CreateDirectory(uatDir);
        File.WriteAllText(Path.Combine(uatDir, "UAT-0001.json"), "null");

        // Act — must not throw
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Empty(result.Records);
    }

    // ── Workspace scoping (isolation between workspaces) ──────────────────────

    [Fact]
    public async Task GetSummaryAsync_TwoWorkspaces_RecordsAreIsolated()
    {
        // Arrange — two separate workspace roots
        var root2 = Path.Combine(Path.GetTempPath(), $"nirmata-uat-tests-ws2-{Guid.NewGuid():N}");
        try
        {
            WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
            {
                id = "UAT-WS1", taskId = "TSK-000001", status = "passed",
            });

            var ws2UatDir = Path.Combine(root2, ".aos", "spec", "uat");
            WriteUatFile(Path.Combine(ws2UatDir, "UAT-0002.json"), new
            {
                id = "UAT-WS2", taskId = "TSK-000002", status = "failed",
            });

            // Act
            var result1 = await _sut.GetSummaryAsync(_root);
            var result2 = await _sut.GetSummaryAsync(root2);

            // Assert — each workspace sees only its own records
            Assert.Single(result1.Records);
            Assert.Equal("UAT-WS1", result1.Records[0].Id);

            Assert.Single(result2.Records);
            Assert.Equal("UAT-WS2", result2.Records[0].Id);
        }
        finally
        {
            if (Directory.Exists(root2))
                Directory.Delete(root2, recursive: true);
        }
    }

    // ── Alternate field names (task / phase aliases) ───────────────────────────

    [Fact]
    public async Task GetSummaryAsync_TaskAlias_ResolvedLikeTaskId()
    {
        // Arrange — use "task" instead of "taskId" (accepted alias per UatFileModel)
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", task = "TSK-000001", status = "passed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Equal("TSK-000001", result.Records[0].TaskId);
        var taskSummary = Assert.Single(result.TaskSummaries);
        Assert.Equal("TSK-000001", taskSummary.TaskId);
    }

    [Fact]
    public async Task GetSummaryAsync_PhaseAlias_ResolvedLikePhaseId()
    {
        // Arrange — use "phase" instead of "phaseId"
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001", taskId = "TSK-000001", phase = "PH-0001", status = "passed",
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        Assert.Equal("PH-0001", result.Records[0].PhaseId);
        var phaseSummary = Assert.Single(result.PhaseSummaries);
        Assert.Equal("PH-0001", phaseSummary.PhaseId);
    }

    // ── Checks collection ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_RecordWithChecks_ChecksAreMapped()
    {
        // Arrange
        WriteUatFile(Path.Combine(UatDir(), "UAT-0001.json"), new
        {
            id = "UAT-0001",
            taskId = "TSK-000001",
            status = "passed",
            checks = new[]
            {
                new { criterionId = "C-1", passed = true, message = "All good" },
                new { criterionId = "C-2", passed = false, message = "Missing output" },
            },
        });

        // Act
        var result = await _sut.GetSummaryAsync(_root);

        // Assert
        var record = Assert.Single(result.Records);
        Assert.Equal(2, record.Checks.Count);
        Assert.Contains(record.Checks, c => c.CriterionId == "C-1" && c.Passed);
        Assert.Contains(record.Checks, c => c.CriterionId == "C-2" && !c.Passed);
    }
}

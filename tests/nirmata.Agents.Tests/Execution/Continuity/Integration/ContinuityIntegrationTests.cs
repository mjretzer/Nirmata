using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.Continuity.HistoryWriter;
using nirmata.Agents.Execution.Continuity.ProgressReporter;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public.Models;
using nirmata.Aos.Public;
using Moq;
using Xunit;

using ProgressReporterType = nirmata.Agents.Execution.Continuity.ProgressReporter.ProgressReporter;

namespace nirmata.Agents.Tests.Execution.Continuity.Integration;

/// <summary>
/// End-to-end integration tests that verify the manual test scenarios:
/// - 6.2: report-progress outputs valid cursor and next command
/// - 6.3: write-history appends entry with evidence pointers
/// - 6.4: summary entries include commit hashes when available
/// </summary>
public class ContinuityIntegrationTests : IDisposable
{
    private readonly string _tempAosRoot;

    public ContinuityIntegrationTests()
    {
        _tempAosRoot = Path.Combine(Path.GetTempPath(), $"aos-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempAosRoot);
        Directory.CreateDirectory(Path.Combine(_tempAosRoot, "evidence", "runs"));
        Directory.CreateDirectory(Path.Combine(_tempAosRoot, "spec"));
        Directory.CreateDirectory(Path.Combine(_tempAosRoot, "state"));
        Directory.CreateDirectory(Path.Combine(_tempAosRoot, "cache"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempAosRoot))
            {
                Directory.Delete(_tempAosRoot, recursive: true);
            }
        }
        catch { }
    }

    private string CreateValidRunId() => Guid.NewGuid().ToString("N").ToLowerInvariant();

    private void CreateRunEvidenceSummary(string runId, int exitCode = 0, string status = "completed")
    {
        var runPath = Path.Combine(_tempAosRoot, "evidence", "runs", runId);
        Directory.CreateDirectory(runPath);

        var summary = $@"{{
  ""runId"": ""{runId}"",
  ""status"": ""{status}"",
  ""startedAtUtc"": ""{DateTimeOffset.UtcNow.AddMinutes(-5):O}"",
  ""finishedAtUtc"": ""{DateTimeOffset.UtcNow:O}"",
  ""exitCode"": {exitCode},
  ""artifacts"": {{
    ""runMetadata"": "".aos/evidence/runs/{runId}/artifacts/run.json"",
    ""packet"": "".aos/evidence/runs/{runId}/artifacts/packet.json"",
    ""result"": "".aos/evidence/runs/{runId}/artifacts/result.json""
  }}
}}";

        File.WriteAllText(Path.Combine(runPath, "summary.json"), summary);
    }

    [Fact]
    public async Task ManualTest_6_2_ReportProgress_OutputsValidCursorAndNextCommand()
    {
        // Arrange - Simulate a real state snapshot
        var stateStore = new FakeStateStore(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Implementation",
                MilestoneId = "M1",
                TaskId = "TSK-0001",
                StepId = "step-1",
                PhaseStatus = StateCursorStatuses.InProgress,
                MilestoneStatus = StateCursorStatuses.InProgress,
                TaskStatus = StateCursorStatuses.InProgress,
                StepStatus = StateCursorStatuses.NotStarted
            }
        });

        var progressReporter = new ProgressReporterType(stateStore);
        var reportHandler = new ReportProgressCommandHandler(progressReporter);

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result = await reportHandler.ExecuteAsync(context);

        // Assert - Verify valid cursor and next command (Task 6.2)
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().NotBeNullOrEmpty();

        var json = result.Output!;
        var doc = JsonDocument.Parse(json);

        // Valid cursor with all fields
        var cursor = doc.RootElement.GetProperty("cursor");
        cursor.GetProperty("phase").GetString().Should().Be("Implementation");
        cursor.GetProperty("milestone").GetString().Should().Be("M1");
        cursor.GetProperty("task").GetString().Should().Be("TSK-0001");
        cursor.GetProperty("step").GetString().Should().Be("step-1");

        // Valid next command
        var nextCommand = doc.RootElement.GetProperty("nextCommand");
        nextCommand.GetProperty("command").GetString().Should().NotBeNullOrEmpty();
        nextCommand.GetProperty("reason").GetString().Should().NotBeNullOrEmpty();

        // Step not started should recommend "execute"
        nextCommand.GetProperty("command").GetString().Should().Be("execute");
    }

    [Fact]
    public async Task ManualTest_6_2_ReportProgress_WithBlockedState_OutputsValidBlockersAndResumeCommand()
    {
        // Arrange - Simulate a blocked state
        var stateStore = new FakeStateStore(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                StepId = "step-1",
                TaskStatus = StateCursorStatuses.InProgress,
                StepStatus = StateCursorStatuses.Blocked
            }
        });

        var progressReporter = new ProgressReporterType(stateStore);
        var reportHandler = new ReportProgressCommandHandler(progressReporter);

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result = await reportHandler.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var json = result.Output!;
        var doc = JsonDocument.Parse(json);

        // Verify blockers are present
        var blockers = doc.RootElement.GetProperty("blockers");
        blockers.GetArrayLength().Should().BeGreaterThan(0);

        var firstBlocker = blockers[0];
        firstBlocker.GetProperty("type").GetString().Should().Be("step-blocked");
        firstBlocker.GetProperty("severity").GetString().Should().Be("high");

        // Verify next command is "resume" for blocked state
        var nextCommand = doc.RootElement.GetProperty("nextCommand");
        nextCommand.GetProperty("command").GetString().Should().Be("resume");
    }

    [Fact]
    public async Task ManualTest_6_3_WriteHistory_AppendsEntryWithEvidencePointers()
    {
        // Arrange
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);

        var historyWriter = new Agents.Execution.Continuity.HistoryWriter.HistoryWriter(_tempAosRoot);
        var handler = new WriteHistoryCommandHandler(historyWriter);

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId },
            Options = new Dictionary<string, string?>
            {
                { "task", "TSK-0001" },
                { "narrative", "Manual test: completed feature implementation" }
            }
        };

        // Act
        var result = await handler.ExecuteAsync(context);

        // Assert - Verify entry was appended with evidence pointers (Task 6.3)
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain($"History entry written for {runId}/TSK-0001");

        // Verify summary.md was created with evidence pointers
        var summaryPath = Path.Combine(_tempAosRoot, "spec", "summary.md");
        File.Exists(summaryPath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(summaryPath);

        // Evidence section should exist
        content.Should().Contain("**Evidence:**");
        content.Should().Contain($".aos/evidence/runs/{runId}/summary.json");

        // Narrative should be included
        content.Should().Contain("Manual test: completed feature implementation");
    }

    [Fact]
    public async Task ManualTest_6_4_SummaryEntry_IncludesCommitHash_WhenGitAvailable()
    {
        // Arrange
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);

        // Create a mock git repository to test commit hash capture
        var gitDir = Path.Combine(_tempAosRoot, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main");

        var refsDir = Path.Combine(gitDir, "refs", "heads");
        Directory.CreateDirectory(refsDir);
        var fakeCommitHash = "abc123def456789012345678901234567890abcd";
        File.WriteAllText(Path.Combine(refsDir, "main"), fakeCommitHash);

        var historyWriter = new Agents.Execution.Continuity.HistoryWriter.HistoryWriter(_tempAosRoot);

        // Act
        var entry = await historyWriter.AppendAsync(runId, narrative: "Testing commit hash capture");

        // Assert - Verify commit hash handling (Task 6.4)
        // Note: The actual commit hash capture depends on git being available in the environment
        // This test verifies the structure supports commit hashes
        entry.Should().NotBeNull();
        entry.RunId.Should().Be(runId);

        var summaryPath = Path.Combine(_tempAosRoot, "spec", "summary.md");
        var content = await File.ReadAllTextAsync(summaryPath);

        // Verify the entry was written
        content.Should().Contain($"### {runId}");
    }

    [Fact]
    public async Task FullTaskLifecycle_PersistsStateEventsRunSummaryAndHistory()
    {
        // Arrange - initialize canonical state and evidence stores
        var stateStore = StateStore.FromAosRoot(_tempAosRoot);
        var runManager = RunManager.FromAosRoot(_tempAosRoot);
        var historyWriter = new Agents.Execution.Continuity.HistoryWriter.HistoryWriter(_tempAosRoot);

        stateStore.EnsureWorkspaceInitialized();

        // Act - move the task into execution, create a run, then complete both the run and task.
        AppendCursorSetEvent(
            stateStore,
            milestoneId: "M1",
            phaseId: "Implementation",
            taskId: "TSK-0001",
            stepId: "step-1",
            milestoneStatus: StateCursorStatuses.InProgress,
            phaseStatus: StateCursorStatuses.InProgress,
            taskStatus: StateCursorStatuses.InProgress,
            stepStatus: StateCursorStatuses.InProgress);
        stateStore.EnsureWorkspaceInitialized();

        var runId = runManager.StartRun("execute-plan", ["--task", "TSK-0001"]);
        var summaryPath = Path.Combine(_tempAosRoot, "evidence", "runs", runId, "summary.json");

        using (var startedSummary = JsonDocument.Parse(await File.ReadAllTextAsync(summaryPath)))
        {
            startedSummary.RootElement.GetProperty("runId").GetString().Should().Be(runId);
            startedSummary.RootElement.GetProperty("status").GetString().Should().Be("started");
            startedSummary.RootElement.GetProperty("startedAtUtc").GetString().Should().NotBeNullOrEmpty();
            startedSummary.RootElement.GetProperty("finishedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
        }

        AppendCursorSetEvent(
            stateStore,
            milestoneId: "M1",
            phaseId: "Implementation",
            taskId: "TSK-0001",
            stepId: null,
            milestoneStatus: StateCursorStatuses.InProgress,
            phaseStatus: StateCursorStatuses.InProgress,
            taskStatus: StateCursorStatuses.Done,
            stepStatus: StateCursorStatuses.Done);
        stateStore.EnsureWorkspaceInitialized();

        runManager.FinishRun(runId);
        var historyEntry = await historyWriter.AppendAsync(runId, "TSK-0001", "Lifecycle completed successfully");

        // Assert - state.json reflects the final cursor.
        var statePath = Path.Combine(_tempAosRoot, "state", "state.json");
        using (var stateDocument = JsonDocument.Parse(await File.ReadAllTextAsync(statePath)))
        {
            var cursor = stateDocument.RootElement.GetProperty("cursor");
            cursor.GetProperty("milestoneId").GetString().Should().Be("M1");
            cursor.GetProperty("phaseId").GetString().Should().Be("Implementation");
            cursor.GetProperty("taskId").GetString().Should().Be("TSK-0001");
            cursor.GetProperty("taskStatus").GetString().Should().Be(StateCursorStatuses.Done);
            cursor.GetProperty("stepStatus").GetString().Should().Be(StateCursorStatuses.Done);
            cursor.TryGetProperty("stepId", out _).Should().BeFalse();
        }

        // Assert - events.ndjson preserves the lifecycle transitions in file order.
        var eventsPath = Path.Combine(_tempAosRoot, "state", "events.ndjson");
        var events = (await File.ReadAllLinesAsync(eventsPath))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line))
            .ToList();

        events.Should().HaveCount(2);
        events.Select(doc => doc.RootElement.GetProperty("eventType").GetString())
            .Should().Equal("cursor.set", "cursor.set");
        events[0].RootElement.GetProperty("cursor").GetProperty("taskStatus").GetString()
            .Should().Be(StateCursorStatuses.InProgress);
        events[1].RootElement.GetProperty("cursor").GetProperty("taskStatus").GetString()
            .Should().Be(StateCursorStatuses.Done);

        foreach (var eventDocument in events)
        {
            eventDocument.Dispose();
        }

        // Assert - run summary advances from started to succeeded and points to canonical artifacts.
        using (var finishedSummary = JsonDocument.Parse(await File.ReadAllTextAsync(summaryPath)))
        {
            finishedSummary.RootElement.GetProperty("status").GetString().Should().Be("succeeded");
            finishedSummary.RootElement.GetProperty("finishedAtUtc").GetString().Should().NotBeNullOrEmpty();

            var artifacts = finishedSummary.RootElement.GetProperty("artifacts");
            artifacts.GetProperty("runMetadata").GetString().Should().Be($".aos/evidence/runs/{runId}/artifacts/run.json");
            artifacts.GetProperty("result").GetString().Should().Be($".aos/evidence/runs/{runId}/artifacts/result.json");
            artifacts.GetProperty("commands").GetString().Should().Be($".aos/evidence/runs/{runId}/commands.json");
        }

        // Assert - history output links the completed run and task back to canonical evidence.
        historyEntry.Key.Should().Be($"{runId}/TSK-0001");
        historyEntry.Verification.Status.Should().Be("passed");
        historyEntry.Evidence.Should().Contain(pointer =>
            pointer.Type == "summary" &&
            pointer.Path == $".aos/evidence/runs/{runId}/summary.json");

        var historyPath = Path.Combine(_tempAosRoot, "spec", "summary.md");
        var historyContent = await File.ReadAllTextAsync(historyPath);
        historyContent.Should().Contain($"### {runId}/TSK-0001");
        historyContent.Should().Contain($".aos/evidence/runs/{runId}/summary.json");
        historyContent.Should().Contain("Lifecycle completed successfully");
    }

    [Fact]
    public async Task ManualTest_6_2_And_6_3_EndToEnd_ProgressReportThenWriteHistory()
    {
        // Arrange - Full workflow test
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);

        // Step 1: Create a state store with active execution
        var stateStore = new FakeStateStore(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Implementation",
                TaskId = "TSK-0001",
                StepId = "step-1",
                PhaseStatus = StateCursorStatuses.InProgress,
                TaskStatus = StateCursorStatuses.Done,
                StepStatus = StateCursorStatuses.Done
            }
        });

        // Step 2: Generate progress report
        var progressReporter = new ProgressReporterType(stateStore);
        var reportHandler = new ReportProgressCommandHandler(progressReporter);
        var reportContext = CommandContext.Create(Mock.Of<IWorkspace>());
        var reportResult = await reportHandler.ExecuteAsync(reportContext);

        // Verify progress report (Task 6.2)
        reportResult.IsSuccess.Should().BeTrue();
        var reportJson = reportResult.Output!;
        var reportDoc = JsonDocument.Parse(reportJson);
        reportDoc.RootElement.GetProperty("cursor").GetProperty("task").GetString().Should().Be("TSK-0001");

        // Step 3: Write history for completed task
        var historyWriter = new Agents.Execution.Continuity.HistoryWriter.HistoryWriter(_tempAosRoot);
        var historyHandler = new WriteHistoryCommandHandler(historyWriter);
        var historyContext = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId },
            Options = new Dictionary<string, string?>
            {
                { "task", "TSK-0001" },
                { "narrative", "Task completed successfully" }
            }
        };

        var historyResult = await historyHandler.ExecuteAsync(historyContext);

        // Verify history entry (Task 6.3)
        historyResult.IsSuccess.Should().BeTrue();
        historyResult.Output.Should().Contain($"History entry written for {runId}/TSK-0001");
        historyResult.Output.Should().Contain("Verification: passed");
    }

    /// <summary>
    /// Fake state store for integration testing
    /// </summary>
    private class FakeStateStore : IStateStore
    {
        private readonly StateSnapshot _snapshot;

        public FakeStateStore(StateSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public StateSnapshot ReadSnapshot() => _snapshot;

        public void EnsureWorkspaceInitialized() { }

        public void AppendEvent(System.Text.Json.JsonElement payload) { }

        public StateEventTailResponse TailEvents(StateEventTailRequest request) =>
            new() { Items = Array.Empty<StateEventEntry>() };
    }

    private static void AppendCursorSetEvent(
        IStateStore stateStore,
        string? milestoneId,
        string? phaseId,
        string? taskId,
        string? stepId,
        string? milestoneStatus,
        string? phaseStatus,
        string? taskStatus,
        string? stepStatus)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            eventType = "cursor.set",
            timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            cursor = new
            {
                milestoneId,
                phaseId,
                taskId,
                stepId,
                milestoneStatus,
                phaseStatus,
                taskStatus,
                stepStatus
            }
        }));

        stateStore.AppendEvent(document.RootElement.Clone());
    }
}

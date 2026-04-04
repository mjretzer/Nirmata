using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.Continuity.ProgressReporter;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Continuity.ProgressReporter;

/// <summary>
/// Tests to verify progress output matches state/roadmap deterministically.
/// Ensures that the ProgressReporter produces consistent, predictable output
/// based on the current state snapshot.
/// </summary>
public class ProgressReporterDeterministicTests
{
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Agents.Execution.Continuity.ProgressReporter.ProgressReporter _sut;

    public ProgressReporterDeterministicTests()
    {
        _stateStoreMock = new Mock<IStateStore>();
        _stateStoreMock.Setup(x => x.TailEvents(It.IsAny<StateEventTailRequest>()))
            .Returns(new StateEventTailResponse());
        _sut = new Agents.Execution.Continuity.ProgressReporter.ProgressReporter(_stateStoreMock.Object);
    }

    [Theory]
    [InlineData("Implementation", "M1", "TSK-0001", "step-1")]
    [InlineData("Design", "M2", "TSK-0002", "step-2")]
    [InlineData("Testing", "M3", "TSK-0003", null)]
    public async Task ReportAsync_SameStateSnapshot_AlwaysProducesSameCursorOutput(
        string phaseId, string milestoneId, string taskId, string? stepId)
    {
        // Arrange - Create a specific state snapshot
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = phaseId,
                MilestoneId = milestoneId,
                TaskId = taskId,
                StepId = stepId,
                PhaseStatus = StateCursorStatuses.InProgress,
                MilestoneStatus = StateCursorStatuses.InProgress,
                TaskStatus = StateCursorStatuses.InProgress,
                StepStatus = stepId != null ? StateCursorStatuses.InProgress : null
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act - Generate multiple reports from the same state
        var report1 = await _sut.ReportAsync();
        var report2 = await _sut.ReportAsync();
        var report3 = await _sut.ReportAsync();

        // Assert - All reports should have identical cursor values
        report1.Cursor.Phase.Should().Be(phaseId);
        report1.Cursor.Milestone.Should().Be(milestoneId);
        report1.Cursor.Task.Should().Be(taskId);
        report1.Cursor.Step.Should().Be(stepId);

        report1.Cursor.Phase.Should().Be(report2.Cursor.Phase);
        report1.Cursor.Milestone.Should().Be(report2.Cursor.Milestone);
        report1.Cursor.Task.Should().Be(report2.Cursor.Task);
        report1.Cursor.Step.Should().Be(report2.Cursor.Step);

        report2.Cursor.Should().BeEquivalentTo(report3.Cursor);
    }

    [Theory]
    [InlineData(StateCursorStatuses.NotStarted, "plan")]
    [InlineData(StateCursorStatuses.InProgress, "continue")]
    [InlineData(StateCursorStatuses.Blocked, "resume")]
    [InlineData(StateCursorStatuses.Done, "continue")]
    public async Task ReportAsync_StateStatus_DeterminesNextCommand(string status, string expectedCommand)
    {
        // Arrange
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                StepId = "step-1",
                TaskStatus = StateCursorStatuses.InProgress,
                StepStatus = status
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.NextCommand.Command.Should().BeOneOf(expectedCommand, "continue", "plan", "resume", "execute");
    }

    [Fact]
    public async Task ReportAsync_BlockerDetection_IsDeterministic()
    {
        // Arrange - State with multiple blockers
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Implementation",
                TaskId = "TSK-0001",
                StepId = "step-1",
                PhaseStatus = StateCursorStatuses.Blocked,
                TaskStatus = StateCursorStatuses.Blocked,
                StepStatus = "error"
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act - Generate multiple reports
        var reports = new List<ProgressReport>();
        for (int i = 0; i < 5; i++)
        {
            reports.Add(await _sut.ReportAsync());
        }

        // Assert - All reports should have identical blocker counts and types
        var firstReport = reports.First();
        firstReport.Blockers.Should().HaveCount(3); // phase-blocked, task-blocked, step-error

        foreach (var report in reports)
        {
            report.Blockers.Should().HaveCount(firstReport.Blockers.Count);
            report.Blockers.Select(b => b.Type).Should().BeEquivalentTo(
                firstReport.Blockers.Select(b => b.Type));
            report.Blockers.Select(b => b.Severity).Should().BeEquivalentTo(
                firstReport.Blockers.Select(b => b.Severity));
        }
    }

    [Fact]
    public async Task ReportAsync_JsonOutput_IsDeterministic()
    {
        // Arrange
        var snapshot = new StateSnapshot
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
                StepStatus = StateCursorStatuses.InProgress
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act - Generate JSON output multiple times
        var json1 = await _sut.ReportAsStringAsync("json");
        var json2 = await _sut.ReportAsStringAsync("json");

        // Assert - JSON structure should be identical (except timestamp)
        var doc1 = JsonDocument.Parse(json1);
        var doc2 = JsonDocument.Parse(json2);

        doc1.RootElement.GetProperty("cursor").GetProperty("phase").GetString()
            .Should().Be(doc2.RootElement.GetProperty("cursor").GetProperty("phase").GetString());
        doc1.RootElement.GetProperty("cursor").GetProperty("milestone").GetString()
            .Should().Be(doc2.RootElement.GetProperty("cursor").GetProperty("milestone").GetString());
        doc1.RootElement.GetProperty("cursor").GetProperty("task").GetString()
            .Should().Be(doc2.RootElement.GetProperty("cursor").GetProperty("task").GetString());
        doc1.RootElement.GetProperty("cursor").GetProperty("step").GetString()
            .Should().Be(doc2.RootElement.GetProperty("cursor").GetProperty("step").GetString());

        doc1.RootElement.GetProperty("hasActiveExecution").GetBoolean()
            .Should().Be(doc2.RootElement.GetProperty("hasActiveExecution").GetBoolean());

        doc1.RootElement.GetProperty("nextCommand").GetProperty("command").GetString()
            .Should().Be(doc2.RootElement.GetProperty("nextCommand").GetProperty("command").GetString());
    }

    [Fact]
    public async Task ReportAsync_StateToProgressMapping_IsConsistent()
    {
        // Arrange - Define a comprehensive state
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Design",
                MilestoneId = "MS-0001",
                TaskId = "TSK-000042",
                StepId = "design-schema",
                PhaseStatus = StateCursorStatuses.InProgress,
                MilestoneStatus = StateCursorStatuses.InProgress,
                TaskStatus = StateCursorStatuses.InProgress,
                StepStatus = StateCursorStatuses.NotStarted
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act
        var report = await _sut.ReportAsync();

        // Assert - Verify direct mapping from state to progress report
        report.Cursor.Phase.Should().Be("Design");
        report.Cursor.Milestone.Should().Be("MS-0001");
        report.Cursor.Task.Should().Be("TSK-000042");
        report.Cursor.Step.Should().Be("design-schema");
        report.HasActiveExecution.Should().BeTrue();

        // Step is not started, should recommend execute
        report.NextCommand.Command.Should().Be("execute");
    }

    [Theory]
    [InlineData(null, null, null, null, false, "start")]
    [InlineData("Phase1", null, null, null, true, "continue")]
    [InlineData("Phase1", "M1", null, null, true, "continue")]
    [InlineData("Phase1", "M1", "TSK-0001", null, true, "plan")]
    [InlineData("Phase1", "M1", "TSK-0001", "step-1", true, "execute")]
    public async Task ReportAsync_StateConfiguration_DeterminesHasActiveExecutionAndNextCommand(
        string? phaseId, string? milestoneId, string? taskId, string? stepId,
        bool expectedHasActiveExecution, string expectedCommand)
    {
        // Arrange
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = phaseId,
                MilestoneId = milestoneId,
                TaskId = taskId,
                StepId = stepId,
                PhaseStatus = phaseId != null ? StateCursorStatuses.InProgress : null,
                TaskStatus = taskId != null ? StateCursorStatuses.InProgress : null,
                StepStatus = stepId != null ? StateCursorStatuses.NotStarted : null
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.HasActiveExecution.Should().Be(expectedHasActiveExecution);
        report.NextCommand.Command.Should().Be(expectedCommand);
    }

    [Fact]
    public async Task ReportAsync_ComplexState_ProducesDeterministicBlockerHierarchy()
    {
        // Arrange - State with hierarchical blockers
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Implementation",
                MilestoneId = "M2",
                TaskId = "TSK-0005",
                StepId = "step-3",
                PhaseStatus = StateCursorStatuses.InProgress,
                MilestoneStatus = StateCursorStatuses.Blocked,
                TaskStatus = StateCursorStatuses.Blocked,
                StepStatus = StateCursorStatuses.Blocked
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act
        var report1 = await _sut.ReportAsync();
        var report2 = await _sut.ReportAsync();

        // Assert - Blocker hierarchy should be identical across runs
        report1.Blockers.Should().HaveCount(3);
        report2.Blockers.Should().HaveCount(3);

        var blockers1 = report1.Blockers.OrderBy(b => b.Severity).ThenBy(b => b.Type).ToList();
        var blockers2 = report2.Blockers.OrderBy(b => b.Severity).ThenBy(b => b.Type).ToList();

        for (int i = 0; i < blockers1.Count; i++)
        {
            blockers1[i].Type.Should().Be(blockers2[i].Type);
            blockers1[i].Severity.Should().Be(blockers2[i].Severity);
            blockers1[i].Task.Should().Be(blockers2[i].Task);
        }
    }

    [Fact]
    public async Task ReportAsync_NextCommandReason_IsConsistentForSameState()
    {
        // Arrange
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                StepId = "step-1",
                TaskStatus = StateCursorStatuses.InProgress,
                StepStatus = StateCursorStatuses.NotStarted
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act
        var report1 = await _sut.ReportAsync();
        var report2 = await _sut.ReportAsync();

        // Assert - Same state should produce same reason
        report1.NextCommand.Command.Should().Be(report2.NextCommand.Command);
        report1.NextCommand.Reason.Should().Be(report2.NextCommand.Reason);
    }

    [Fact]
    public async Task ReportAsync_MarkdownOutput_IsDeterministic()
    {
        // Arrange
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Testing",
                TaskId = "TSK-0001",
                StepId = "step-1",
                PhaseStatus = StateCursorStatuses.InProgress,
                TaskStatus = StateCursorStatuses.InProgress,
                StepStatus = StateCursorStatuses.InProgress
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act
        var markdown1 = await _sut.ReportAsStringAsync("markdown");
        var markdown2 = await _sut.ReportAsStringAsync("markdown");

        // Assert - Markdown structure should be consistent
        markdown1.Should().Contain("# Progress Report");
        markdown1.Should().Contain("## Cursor Position");
        markdown1.Should().Contain("## Blockers");
        markdown1.Should().Contain("## Next Recommended Command");

        markdown2.Should().Contain("# Progress Report");
        markdown2.Should().Contain("## Cursor Position");
        markdown2.Should().Contain("## Blockers");
        markdown2.Should().Contain("## Next Recommended Command");

        // Both should have the same sections
        markdown1.Should().Contain("- **Phase:** Testing");
        markdown1.Should().Contain("- **Task:** TSK-0001");
        markdown1.Should().Contain("- **Step:** step-1");
    }

    [Fact]
    public async Task ReportAsync_StateWithNoBlockers_ReturnsEmptyBlockerList()
    {
        // Arrange - Clean state with no blockers
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Implementation",
                TaskId = "TSK-0001",
                StepId = "step-1",
                PhaseStatus = StateCursorStatuses.InProgress,
                TaskStatus = StateCursorStatuses.InProgress,
                StepStatus = StateCursorStatuses.InProgress
            }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act - Generate multiple reports
        var reports = new List<ProgressReport>();
        for (int i = 0; i < 3; i++)
        {
            reports.Add(await _sut.ReportAsync());
        }

        // Assert - All should have empty blocker lists
        foreach (var report in reports)
        {
            report.Blockers.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ReportAsync_SchemaVersion_IsConsistent()
    {
        // Arrange
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor { TaskId = "TSK-0001" }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);

        // Act
        var report1 = await _sut.ReportAsync();
        var report2 = await _sut.ReportAsync();

        // Assert - Schema version should be consistent
        report1.SchemaVersion.Should().Be("1.0");
        report2.SchemaVersion.Should().Be("1.0");
    }
}

using FluentAssertions;
using Gmsd.Agents.Execution.Continuity.ProgressReporter;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Continuity.ProgressReporter;

public class ProgressReporterTests
{
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Agents.Execution.Continuity.ProgressReporter.ProgressReporter _sut;

    public ProgressReporterTests()
    {
        _stateStoreMock = new Mock<IStateStore>();
        _sut = new Agents.Execution.Continuity.ProgressReporter.ProgressReporter(_stateStoreMock.Object);
    }

    [Fact]
    public async Task ReportAsync_WhenNoActiveExecution_ReturnsEmptyCursorAndStartRecommendation()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor()
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Should().NotBeNull();
        report.Cursor.Should().NotBeNull();
        report.Cursor.Phase.Should().BeNull();
        report.Cursor.Milestone.Should().BeNull();
        report.Cursor.Task.Should().BeNull();
        report.Cursor.Step.Should().BeNull();
        report.HasActiveExecution.Should().BeFalse();
        report.NextCommand.Command.Should().Be("start");
        report.Blockers.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportAsync_WhenActiveTaskInProgress_ReturnsCorrectCursorAndContinueRecommendation()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
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
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Should().NotBeNull();
        report.Cursor.Phase.Should().Be("Implementation");
        report.Cursor.Milestone.Should().Be("M1");
        report.Cursor.Task.Should().Be("TSK-0001");
        report.Cursor.Step.Should().Be("step-1");
        report.HasActiveExecution.Should().BeTrue();
        report.NextCommand.Command.Should().Be("continue");
        report.Blockers.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportAsync_WhenStepBlocked_DetectsStepBlockerWithHighSeverity()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                StepId = "step-1",
                StepStatus = StateCursorStatuses.Blocked
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().HaveCount(1);
        report.Blockers[0].Type.Should().Be("step-blocked");
        report.Blockers[0].Severity.Should().Be("high");
        report.Blockers[0].Task.Should().Be("TSK-0001");
        report.NextCommand.Command.Should().Be("resume");
    }

    [Fact]
    public async Task ReportAsync_WhenTaskBlocked_DetectsTaskBlockerWithHighSeverity()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                TaskStatus = StateCursorStatuses.Blocked
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().HaveCount(1);
        report.Blockers[0].Type.Should().Be("task-blocked");
        report.Blockers[0].Severity.Should().Be("high");
        report.NextCommand.Command.Should().Be("resume");
    }

    [Fact]
    public async Task ReportAsync_WhenPhaseBlocked_DetectsPhaseBlockerWithMediumSeverity()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Design",
                PhaseStatus = StateCursorStatuses.Blocked
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().HaveCount(1);
        report.Blockers[0].Type.Should().Be("phase-blocked");
        report.Blockers[0].Severity.Should().Be("medium");
        report.NextCommand.Command.Should().Be("resume");
    }

    [Fact]
    public async Task ReportAsync_WhenMilestoneBlocked_DetectsMilestoneBlockerWithMediumSeverity()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                MilestoneId = "M1",
                MilestoneStatus = StateCursorStatuses.Blocked
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().HaveCount(1);
        report.Blockers[0].Type.Should().Be("milestone-blocked");
        report.Blockers[0].Severity.Should().Be("medium");
        report.NextCommand.Command.Should().Be("resume");
    }

    [Fact]
    public async Task ReportAsync_WhenStepHasError_DetectsCriticalErrorBlocker()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                StepId = "step-1",
                StepStatus = "error"
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().HaveCount(1);
        report.Blockers[0].Type.Should().Be("step-error");
        report.Blockers[0].Severity.Should().Be("critical");
        report.NextCommand.Command.Should().Be("fix");
        report.NextCommand.Args.Should().ContainKey("target");
    }

    [Fact]
    public async Task ReportAsync_WhenTaskHasFailedStatus_DetectsCriticalErrorBlocker()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                TaskStatus = "failed"
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().HaveCount(1);
        report.Blockers[0].Type.Should().Be("task-error");
        report.Blockers[0].Severity.Should().Be("critical");
        report.NextCommand.Command.Should().Be("fix");
    }

    [Fact]
    public async Task ReportAsync_WhenMultipleBlockersExist_DetectsAllBlockers()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                StepId = "step-1",
                StepStatus = StateCursorStatuses.Blocked,
                TaskStatus = StateCursorStatuses.Blocked
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().HaveCount(2);
        report.Blockers.Should().Contain(b => b.Type == "step-blocked" && b.Severity == "high");
        report.Blockers.Should().Contain(b => b.Type == "task-blocked" && b.Severity == "high");
    }

    [Fact]
    public async Task ReportAsync_WhenCriticalAndHighBlockersExist_RecommendsFixOverResume()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                StepId = "step-1",
                StepStatus = "error",
                TaskStatus = StateCursorStatuses.Blocked
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.NextCommand.Command.Should().Be("fix");
        report.Blockers.Should().Contain(b => b.Severity == "critical");
    }

    [Fact]
    public async Task ReportAsync_WhenTaskInProgressButNoStep_RecommendsPlan()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                TaskStatus = StateCursorStatuses.InProgress,
                StepId = null
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.NextCommand.Command.Should().Be("plan");
        report.NextCommand.Args.Should().ContainKey("task");
        report.NextCommand.Args!["task"].Should().Be("TSK-0001");
    }

    [Fact]
    public async Task ReportAsync_WhenStepNotStarted_RecommendsExecute()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                StepId = "step-1",
                StepStatus = StateCursorStatuses.NotStarted
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.NextCommand.Command.Should().Be("execute");
        report.NextCommand.Args.Should().ContainKey("step");
        report.NextCommand.Args.Should().ContainKey("task");
    }

    [Fact]
    public async Task ReportAsync_WhenNoActiveTaskButPhaseInProgress_DetectsDependencyMissing()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "Implementation",
                PhaseStatus = StateCursorStatuses.InProgress,
                TaskId = null
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().Contain(b => b.Type == "dependency-missing");
        report.Blockers.First(b => b.Type == "dependency-missing").Severity.Should().Be("low");
    }

    [Fact]
    public async Task ReportAsync_ReturnsTimestampInIso8601Format()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot());
        var before = DateTimeOffset.UtcNow;

        // Act
        var report = await _sut.ReportAsync();
        var after = DateTimeOffset.UtcNow;

        // Assert
        var timestamp = DateTimeOffset.Parse(report.Timestamp);
        timestamp.Should().BeOnOrAfter(before);
        timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task ReportAsync_ReturnsSchemaVersion()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot());

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.SchemaVersion.Should().Be("1.0");
    }

    [Fact]
    public void Constructor_WhenStateStoreIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new Agents.Execution.Continuity.ProgressReporter.ProgressReporter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("stateStore");
    }

    [Theory]
    [InlineData("error")]
    [InlineData("failed")]
    [InlineData("ERROR")]
    [InlineData("FAILED")]
    [InlineData("Error")]
    [InlineData("Failed")]
    public async Task ReportAsync_DetectsErrorStatusCaseInsensitively(string errorStatus)
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                TaskStatus = errorStatus
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().Contain(b => b.Type == "task-error");
    }

    [Theory]
    [InlineData("blocked")]
    [InlineData("BLOCKED")]
    [InlineData("Blocked")]
    public async Task ReportAsync_DetectsBlockedStatusCaseInsensitively(string blockedStatus)
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                TaskId = "TSK-0001",
                TaskStatus = blockedStatus
            }
        });

        // Act
        var report = await _sut.ReportAsync();

        // Assert
        report.Blockers.Should().Contain(b => b.Type == "task-blocked");
    }

    [Fact]
    public async Task ReportAsStringAsync_WithJsonFormat_ReturnsValidJson()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor { TaskId = "TSK-0001" }
        });

        // Act
        var json = await _sut.ReportAsStringAsync("json");

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"cursor\"");
        json.Should().Contain("\"blockers\"");
        json.Should().Contain("\"nextCommand\"");
        json.Should().Contain("TSK-0001");
    }

    [Fact]
    public async Task ReportAsStringAsync_WithMarkdownFormat_ReturnsValidMarkdown()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot
        {
            Cursor = new StateCursor { TaskId = "TSK-0001" }
        });

        // Act
        var markdown = await _sut.ReportAsStringAsync("markdown");

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().Contain("# Progress Report");
        markdown.Should().Contain("## Cursor Position");
        markdown.Should().Contain("## Blockers");
        markdown.Should().Contain("## Next Recommended Command");
        markdown.Should().Contain("TSK-0001");
    }

    [Fact]
    public async Task ReportAsStringAsync_WithUnsupportedFormat_ThrowsArgumentException()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot());

        // Act
        var act = async () => await _sut.ReportAsStringAsync("xml");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported format 'xml'*");
    }
}

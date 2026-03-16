using FluentAssertions;
using nirmata.Agents.Execution.Continuity;
using nirmata.Agents.Models.Results;
using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Persistence.State;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Continuity;

public class PauseResumeManagerTests : IDisposable
{
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IRunRepository> _runRepositoryMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IHandoffStateStore> _handoffStateStoreMock;
    private readonly PauseResumeManager _sut;
    private readonly TempDirectory _tempDir;

    public PauseResumeManagerTests()
    {
        _stateStoreMock = new Mock<IStateStore>();
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _runRepositoryMock = new Mock<IRunRepository>();
        _workspaceMock = new Mock<IWorkspace>();
        _handoffStateStoreMock = new Mock<IHandoffStateStore>();

        _tempDir = new TempDirectory();
        _workspaceMock.Setup(x => x.AosRootPath).Returns(_tempDir.Path);

        _sut = new PauseResumeManager(
            _stateStoreMock.Object,
            _runLifecycleManagerMock.Object,
            _runRepositoryMock.Object,
            _workspaceMock.Object,
            _handoffStateStoreMock.Object);
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    [Fact]
    public async Task PauseAsync_WhenNoActiveTask_ThrowsInvalidOperationException()
    {
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(new StateSnapshot());

        var act = async () => await _sut.PauseAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No active execution found*");
    }

    [Fact]
    public async Task PauseAsync_WhenActiveTaskExists_WritesHandoffAndReturnsMetadata()
    {
        // Arrange
        var runId = "RUN-20260101-120000-abc123";
        var taskId = "TSK-0001";
        var snapshot = new StateSnapshot
        {
            Cursor = new StateCursor { TaskId = taskId, PhaseId = "Implementation" }
        };
        _stateStoreMock.Setup(x => x.ReadSnapshot()).Returns(snapshot);
        _runRepositoryMock.Setup(x => x.ListAsync(It.IsAny<string?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunResponse>
            {
                new() { RunId = runId, StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
            });
        _handoffStateStoreMock.Setup(x => x.HandoffPath).Returns("/tmp/aos/.aos/state/handoff.json");

        // Act
        var result = await _sut.PauseAsync("user interruption");

        // Assert
        result.Should().NotBeNull();
        result.SourceRunId.Should().Be(runId);
        result.Reason.Should().Be("user interruption");
        result.HandoffPath.Should().Be("/tmp/aos/.aos/state/handoff.json");
        _handoffStateStoreMock.Verify(x => x.WriteHandoff(It.Is<HandoffState>(h =>
            h.SourceRunId == runId &&
            h.Cursor.TaskId == taskId &&
            h.Reason == "user interruption" &&
            h.SchemaVersion == "1.0"
        )), Times.Once);
        _runLifecycleManagerMock.Verify(x => x.RecordCommandAsync(
            runId, "continuity", "pause", "completed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeAsync_WhenNoHandoff_ThrowsException()
    {
        _handoffStateStoreMock.Setup(x => x.Exists()).Returns(false);

        var act = async () => await _sut.ResumeAsync();

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*handoff validation failed*");
    }

    [Fact]
    public async Task ResumeAsync_WhenValidHandoff_StartsNewRunAndReturnsResult()
    {
        // Arrange
        var sourceRunId = "RUN-20260101-120000-abc123";
        var newRunId = "RUN-20260101-130000-def456";
        var handoff = new HandoffState
        {
            SchemaVersion = "1.0",
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            SourceRunId = sourceRunId,
            Cursor = new StateCursor { TaskId = "TSK-0001", PhaseId = "Implementation" },
            TaskContext = new TaskContext { TaskId = "TSK-0001", Status = "paused" },
            Scope = new ScopeConstraints(),
            NextCommand = new NextCommand { Name = "continue" }
        };
        _handoffStateStoreMock.Setup(x => x.Exists()).Returns(true);
        _handoffStateStoreMock.Setup(x => x.ReadHandoff()).Returns(handoff);
        _runLifecycleManagerMock.Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunContext { RunId = newRunId });

        // Act
        var result = await _sut.ResumeAsync();

        // Assert
        result.Should().NotBeNull();
        result.RunId.Should().Be(newRunId);
        result.SourceRunId.Should().Be(sourceRunId);
        result.Status.Should().Be(ResumeStatus.Success);
        result.Cursor.TaskId.Should().Be("TSK-0001");
    }

    [Fact]
    public async Task ResumeFromRunAsync_WhenRunNotFound_ThrowsDirectoryNotFoundException()
    {
        _runRepositoryMock.Setup(x => x.ExistsAsync("RUN-xyz123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = async () => await _sut.ResumeFromRunAsync("RUN-xyz123");

        await act.Should().ThrowAsync<DirectoryNotFoundException>()
            .WithMessage("Run 'RUN-xyz123' not found*");
    }

    [Fact]
    public async Task ResumeFromRunAsync_WhenRunExists_CreatesHandoffAndResumes()
    {
        // Arrange
        var historicalRunId = "RUN-20260101-120000-abc123";
        var newRunId = "RUN-20260101-130000-def456";
        
        // Create evidence directory
        var evidencePath = Path.Combine(_tempDir.Path, "evidence", "runs", historicalRunId);
        Directory.CreateDirectory(evidencePath);

        _runRepositoryMock.Setup(x => x.ExistsAsync(historicalRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _runRepositoryMock.Setup(x => x.GetAsync(historicalRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunResponse
            {
                RunId = historicalRunId,
                StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
                Success = true
            });
        _runLifecycleManagerMock.Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunContext { RunId = newRunId });

        // Act
        var result = await _sut.ResumeFromRunAsync(historicalRunId);

        // Assert
        result.Should().NotBeNull();
        result.RunId.Should().Be(newRunId);
        result.SourceRunId.Should().Be(historicalRunId);
        result.Status.Should().Be(ResumeStatus.Success);
        _handoffStateStoreMock.Verify(x => x.WriteHandoff(It.Is<HandoffState>(h =>
            h.SourceRunId == historicalRunId &&
            h.ContinuationRunId == newRunId
        )), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ValidateHandoffAsync_WhenNoHandoff_ReturnsInvalid()
    {
        _handoffStateStoreMock.Setup(x => x.Exists()).Returns(false);

        var result = await _sut.ValidateHandoffAsync();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("No handoff.json file exists.");
    }

    [Fact]
    public async Task ValidateHandoffAsync_WhenSchemaVersionMissing_ReturnsInvalid()
    {
        var handoff = new HandoffState
        {
            SchemaVersion = null!,
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            SourceRunId = "RUN-123",
            Cursor = new StateCursor(),
            TaskContext = new TaskContext { TaskId = "TSK-1", Status = "paused" },
            Scope = new ScopeConstraints(),
            NextCommand = new NextCommand { Name = "test" }
        };
        _handoffStateStoreMock.Setup(x => x.Exists()).Returns(true);
        _handoffStateStoreMock.Setup(x => x.ReadHandoff()).Returns(handoff);

        var result = await _sut.ValidateHandoffAsync();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing schemaVersion field.");
    }

    [Fact]
    public async Task ValidateHandoffAsync_WhenUnsupportedSchemaVersion_ReturnsInvalid()
    {
        var handoff = new HandoffState
        {
            SchemaVersion = "2.0",
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            SourceRunId = "RUN-123",
            Cursor = new StateCursor(),
            TaskContext = new TaskContext { TaskId = "TSK-1", Status = "paused" },
            Scope = new ScopeConstraints(),
            NextCommand = new NextCommand { Name = "test" }
        };
        _handoffStateStoreMock.Setup(x => x.Exists()).Returns(true);
        _handoffStateStoreMock.Setup(x => x.ReadHandoff()).Returns(handoff);

        var result = await _sut.ValidateHandoffAsync();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Unsupported schemaVersion"));
    }

    [Fact]
    public async Task ValidateHandoffAsync_WhenValid_ReturnsValidWithHandoff()
    {
        var handoff = new HandoffState
        {
            SchemaVersion = "1.0",
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            SourceRunId = "RUN-123",
            Cursor = new StateCursor { TaskId = "TSK-1" },
            TaskContext = new TaskContext { TaskId = "TSK-1", Status = "paused" },
            Scope = new ScopeConstraints(),
            NextCommand = new NextCommand { Name = "test" }
        };
        _handoffStateStoreMock.Setup(x => x.Exists()).Returns(true);
        _handoffStateStoreMock.Setup(x => x.ReadHandoff()).Returns(handoff);

        var result = await _sut.ValidateHandoffAsync();

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Handoff.Should().NotBeNull();
        result.Handoff!.SourceRunId.Should().Be("RUN-123");
    }
}

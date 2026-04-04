using FluentAssertions;
using nirmata.Agents.Execution.Continuity;
using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Continuity;

public class ResumeWorkCommandHandlerTests
{
    private readonly Mock<IPauseResumeManager> _pauseResumeManagerMock;
    private readonly ResumeWorkCommandHandler _sut;

    public ResumeWorkCommandHandlerTests()
    {
        _pauseResumeManagerMock = new Mock<IPauseResumeManager>();
        _sut = new ResumeWorkCommandHandler(_pauseResumeManagerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenValidHandoff_ReturnsSuccessWithRunId()
    {
        var request = CommandRequest.Create("continuity", "resume-work");
        var handoff = new HandoffState
        {
            SchemaVersion = "1.0",
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            SourceRunId = "RUN-20260101-120000-abc123",
            Cursor = new StateCursor { TaskId = "TSK-0001", PhaseId = "Implementation" },
            TaskContext = new TaskContext { TaskId = "TSK-0001", Status = "paused" },
            Scope = new ScopeConstraints(),
            NextCommand = new NextCommand { Name = "continue" }
        };
        _pauseResumeManagerMock.Setup(x => x.ValidateHandoffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffValidationResult { IsValid = true, Handoff = handoff });
        _pauseResumeManagerMock.Setup(x => x.ResumeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeResult
            {
                RunId = "RUN-20260101-130000-def456",
                SourceRunId = "RUN-20260101-120000-abc123",
                Status = ResumeStatus.Success,
                Cursor = handoff.Cursor
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("resumed successfully");
        result.Output.Should().Contain("RUN-20260101-130000-def456");
        result.Output.Should().Contain("TSK-0001");
        result.RoutingHint.Should().Be("Orchestrator");
    }

    [Fact]
    public async Task HandleAsync_WhenValidationFails_ReturnsFailureWithSuggestion()
    {
        var request = CommandRequest.Create("continuity", "resume-work");
        _pauseResumeManagerMock.Setup(x => x.ValidateHandoffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffValidationResult
            {
                IsValid = false,
                Errors = new[] { "Missing cursor field" }
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("handoff validation failed");
        result.ErrorOutput.Should().Contain("Missing cursor field");
        result.ErrorOutput.Should().Contain("start-run");
    }

    [Fact]
    public async Task HandleAsync_WhenNoHandoff_ReturnsFailureWithSuggestion()
    {
        var request = CommandRequest.Create("continuity", "resume-work");
        _pauseResumeManagerMock.Setup(x => x.ValidateHandoffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffValidationResult { IsValid = true });
        _pauseResumeManagerMock.Setup(x => x.ResumeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("No handoff.json found");
        result.ErrorOutput.Should().Contain("start-run");
    }

    [Fact]
    public async Task HandleAsync_WhenInvalidData_ReturnsFailure()
    {
        var request = CommandRequest.Create("continuity", "resume-work");
        _pauseResumeManagerMock.Setup(x => x.ValidateHandoffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffValidationResult { IsValid = true });
        _pauseResumeManagerMock.Setup(x => x.ResumeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("Corrupted handoff"));

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Handoff data is invalid");
        result.ErrorOutput.Should().Contain("Corrupted handoff");
    }

    [Fact]
    public async Task HandleAsync_WithWarnings_ReturnsSuccessWithWarningMessage()
    {
        var request = CommandRequest.Create("continuity", "resume-work");
        var handoff = new HandoffState
        {
            SchemaVersion = "1.0",
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            SourceRunId = "RUN-20260101-120000-abc123",
            Cursor = new StateCursor { TaskId = "TSK-0001" },
            TaskContext = new TaskContext { TaskId = "TSK-0001", Status = "paused" },
            Scope = new ScopeConstraints(),
            NextCommand = new NextCommand { Name = "continue" }
        };
        _pauseResumeManagerMock.Setup(x => x.ValidateHandoffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffValidationResult { IsValid = true, Handoff = handoff });
        _pauseResumeManagerMock.Setup(x => x.ResumeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeResult
            {
                RunId = "RUN-20260101-130000-def456",
                SourceRunId = "RUN-20260101-120000-abc123",
                Status = ResumeStatus.SuccessWithWarnings,
                Cursor = handoff.Cursor
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("with warnings");
    }

    [Fact]
    public async Task HandleAsync_WhenFailedStatus_ReturnsFailure()
    {
        var request = CommandRequest.Create("continuity", "resume-work");
        var handoff = new HandoffState
        {
            SchemaVersion = "1.0",
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            SourceRunId = "RUN-20260101-120000-abc123",
            Cursor = new StateCursor { TaskId = "TSK-0001" },
            TaskContext = new TaskContext { TaskId = "TSK-0001", Status = "paused" },
            Scope = new ScopeConstraints(),
            NextCommand = new NextCommand { Name = "continue" }
        };
        _pauseResumeManagerMock.Setup(x => x.ValidateHandoffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffValidationResult { IsValid = true, Handoff = handoff });
        _pauseResumeManagerMock.Setup(x => x.ResumeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeResult
            {
                RunId = "RUN-20260101-130000-def456",
                SourceRunId = "RUN-20260101-120000-abc123",
                Status = ResumeStatus.Failed,
                Cursor = handoff.Cursor
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
    }
}

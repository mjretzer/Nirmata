using FluentAssertions;
using Gmsd.Agents.Execution.Continuity;
using Gmsd.Aos.Contracts.Commands;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Continuity;

public class PauseWorkCommandHandlerTests
{
    private readonly Mock<IPauseResumeManager> _pauseResumeManagerMock;
    private readonly PauseWorkCommandHandler _sut;

    public PauseWorkCommandHandlerTests()
    {
        _pauseResumeManagerMock = new Mock<IPauseResumeManager>();
        _sut = new PauseWorkCommandHandler(_pauseResumeManagerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenNoReason_PausesWithoutReason()
    {
        var request = CommandRequest.Create("continuity", "pause-work");
        _pauseResumeManagerMock.Setup(x => x.PauseAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffMetadata
            {
                Timestamp = "2026-01-01T12:00:00Z",
                SourceRunId = "RUN-123",
                HandoffPath = "/tmp/aos/.aos/state/handoff.json"
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("paused successfully");
        result.Output.Should().Contain("RUN-123");
        _pauseResumeManagerMock.Verify(x => x.PauseAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithReasonOption_PausesWithReason()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "pause-work",
            Options = new Dictionary<string, string?> { { "reason", "user interruption" } }
        };
        _pauseResumeManagerMock.Setup(x => x.PauseAsync("user interruption", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffMetadata
            {
                Timestamp = "2026-01-01T12:00:00Z",
                SourceRunId = "RUN-123",
                HandoffPath = "/tmp/aos/.aos/state/handoff.json",
                Reason = "user interruption"
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("Reason: user interruption");
        _pauseResumeManagerMock.Verify(x => x.PauseAsync("user interruption", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithReasonArgument_PausesWithReason()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "pause-work",
            Arguments = new[] { "--reason=\"lunch break\"" }
        };

        _pauseResumeManagerMock.Setup(x => x.PauseAsync("lunch break", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HandoffMetadata
            {
                Timestamp = "2026-01-01T12:00:00Z",
                SourceRunId = "RUN-123",
                HandoffPath = "/tmp/aos/.aos/state/handoff.json",
                Reason = "lunch break"
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeTrue();
        _pauseResumeManagerMock.Verify(x => x.PauseAsync("lunch break", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNoActiveExecution_ReturnsFailure()
    {
        var request = CommandRequest.Create("continuity", "pause-work");
        _pauseResumeManagerMock.Setup(x => x.PauseAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No active execution found"));

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("No active execution found");
    }

    [Fact]
    public async Task HandleAsync_WhenException_ReturnsFailureWithMessage()
    {
        var request = CommandRequest.Create("continuity", "pause-work");
        _pauseResumeManagerMock.Setup(x => x.PauseAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Disk full"));

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Pause operation failed");
        result.ErrorOutput.Should().Contain("Disk full");
    }
}

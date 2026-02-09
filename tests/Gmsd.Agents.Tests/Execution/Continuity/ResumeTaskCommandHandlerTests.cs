using FluentAssertions;
using Gmsd.Agents.Execution.Continuity;
using Gmsd.Aos.Contracts.Commands;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Continuity;

public class ResumeTaskCommandHandlerTests
{
    private readonly Mock<IPauseResumeManager> _pauseResumeManagerMock;
    private readonly ResumeTaskCommandHandler _sut;

    public ResumeTaskCommandHandlerTests()
    {
        _pauseResumeManagerMock = new Mock<IPauseResumeManager>();
        _sut = new ResumeTaskCommandHandler(_pauseResumeManagerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithRunIdOption_ReturnsSuccess()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "resume-task",
            Options = new Dictionary<string, string?> { { "run-id", "RUN-20260101-120000-abc123" } }
        };
        _pauseResumeManagerMock.Setup(x => x.ResumeFromRunAsync("RUN-20260101-120000-abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeResult
            {
                RunId = "RUN-20260101-130000-def456",
                SourceRunId = "RUN-20260101-120000-abc123",
                Status = ResumeStatus.Success,
                Cursor = new()
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("RUN-20260101-120000-abc123");
        result.Output.Should().Contain("RUN-20260101-130000-def456");
        result.RoutingHint.Should().Be("Orchestrator");
    }

    [Fact]
    public async Task HandleAsync_WithRunIdArgument_ReturnsSuccess()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "resume-task",
            Arguments = new[] { "--run-id=RUN-20260101-120000-abc123" }
        };
        _pauseResumeManagerMock.Setup(x => x.ResumeFromRunAsync("RUN-20260101-120000-abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeResult
            {
                RunId = "RUN-20260101-130000-def456",
                SourceRunId = "RUN-20260101-120000-abc123",
                Status = ResumeStatus.Success,
                Cursor = new()
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithPositionalArgument_ReturnsSuccess()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "resume-task",
            Arguments = new[] { "RUN-20260101-120000-abc123" }
        };
        _pauseResumeManagerMock.Setup(x => x.ResumeFromRunAsync("RUN-20260101-120000-abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeResult
            {
                RunId = "RUN-20260101-130000-def456",
                SourceRunId = "RUN-20260101-120000-abc123",
                Status = ResumeStatus.Success,
                Cursor = new()
            });

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithoutRunId_ReturnsFailureWithUsage()
    {
        var request = CommandRequest.Create("continuity", "resume-task");

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("--run-id");
        result.ErrorOutput.Should().Contain("Usage");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidRunIdFormat_ReturnsFailure()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "resume-task",
            Arguments = new[] { "invalid-run-id" }
        };

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Invalid run-id format");
    }

    [Fact]
    public async Task HandleAsync_WhenRunNotFound_ReturnsFailureWithSuggestion()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "resume-task",
            Arguments = new[] { "RUN-xyz123" }
        };
        _pauseResumeManagerMock.Setup(x => x.ResumeFromRunAsync("RUN-xyz123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException("Run not found"));

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Run not found");
        result.ErrorOutput.Should().Contain("list-runs");
    }

    [Fact]
    public async Task HandleAsync_WhenCorrupted_ReturnsFailure()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "resume-task",
            Arguments = new[] { "RUN-20260101-120000-abc123" }
        };
        _pauseResumeManagerMock.Setup(x => x.ResumeFromRunAsync("RUN-20260101-120000-abc123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("Corrupted evidence"));

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("corrupted");
    }

    [Fact]
    public async Task HandleAsync_WhenException_ReturnsFailureWithMessage()
    {
        var request = new CommandRequest
        {
            Group = "continuity",
            Command = "resume-task",
            Arguments = new[] { "RUN-20260101-120000-abc123" }
        };
        _pauseResumeManagerMock.Setup(x => x.ResumeFromRunAsync("RUN-20260101-120000-abc123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var result = await _sut.HandleAsync(request, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Resume-task operation failed");
    }
}

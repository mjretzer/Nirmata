using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Observability;
using Gmsd.Web.AgentRunner;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Gmsd.Web.Tests.AgentRunner;

public class WorkflowClassifierTests
{
    private readonly Mock<IOrchestrator> _orchestratorMock;
    private readonly Mock<ICorrelationIdProvider> _correlationIdProviderMock;
    private readonly Mock<ILogger<WorkflowClassifier>> _loggerMock;
    private readonly WorkflowClassifier _sut;

    public WorkflowClassifierTests()
    {
        _orchestratorMock = new Mock<IOrchestrator>();
        _correlationIdProviderMock = new Mock<ICorrelationIdProvider>();
        _loggerMock = new Mock<ILogger<WorkflowClassifier>>();

        _sut = new WorkflowClassifier(
            _orchestratorMock.Object,
            _correlationIdProviderMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public void Constructor_WhenOrchestratorIsNull_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowClassifier(
            null!,
            _correlationIdProviderMock.Object,
            _loggerMock.Object
        );

        act.Should().Throw<ArgumentNullException>().WithParameterName("orchestrator");
    }

    [Fact]
    public void Constructor_WhenCorrelationIdProviderIsNull_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowClassifier(
            _orchestratorMock.Object,
            null!,
            _loggerMock.Object
        );

        act.Should().Throw<ArgumentNullException>().WithParameterName("correlationIdProvider");
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowClassifier(
            _orchestratorMock.Object,
            _correlationIdProviderMock.Object,
            null!
        );

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsNull_ThrowsArgumentException()
    {
        var act = () => _sut.ExecuteAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("inputRaw")
            .WithMessage("*cannot be null or whitespace*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsEmpty_ThrowsArgumentException()
    {
        var act = () => _sut.ExecuteAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("inputRaw")
            .WithMessage("*cannot be null or whitespace*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsWhitespace_ThrowsArgumentException()
    {
        var act = () => _sut.ExecuteAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("inputRaw")
            .WithMessage("*cannot be null or whitespace*");
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesCorrelationId()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.CorrelationId == testCorrelationId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("test command");

        _correlationIdProviderMock.Verify(x => x.Generate(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SetsCurrentCorrelationId()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("test command");

        _correlationIdProviderMock.Verify(x => x.SetCurrent(testCorrelationId), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesNormalizedInputToOrchestrator()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputRaw == "test command" &&
                 w.InputNormalized == "run test command" &&
                 w.CorrelationId == testCorrelationId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("test command");

        _orchestratorMock.Verify(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputRaw == "test command" &&
                 w.InputNormalized == "run test command"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputAlreadyHasRunPrefix_DoesNotNormalize()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputRaw == "run something" &&
                 w.InputNormalized == null &&
                 w.CorrelationId == testCorrelationId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("run something");

        _orchestratorMock.Verify(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputNormalized == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputHasPlanPrefix_DoesNotNormalize()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputRaw == "plan something" &&
                 w.InputNormalized == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("plan something");

        _orchestratorMock.Verify(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputNormalized == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsHelp_DoesNotNormalize()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputRaw == "help" &&
                 w.InputNormalized == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("help");

        _orchestratorMock.Verify(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputNormalized == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsStatus_DoesNotNormalize()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputRaw == "status" &&
                 w.InputNormalized == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("status");

        _orchestratorMock.Verify(x => x.ExecuteAsync(It.Is<WorkflowIntent>(
            w => w.InputNormalized == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ReturnsOrchestratorResult()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await _sut.ExecuteAsync("test command");

        result.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_OnFailure_ReturnsOrchestratorResult()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = false,
            RunId = "run-001",
            FinalPhase = "Failed"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await _sut.ExecuteAsync("test command");

        result.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.IsAny<WorkflowIntent>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.ExecuteAsync("test command", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_OnException_LogsErrorAndRethrows()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var exception = new InvalidOperationException("Test error");
        _orchestratorMock.Setup(x => x.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var act = () => _sut.ExecuteAsync("test command");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test error");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Agent execution failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_LogsInformation()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            RunId = "run-001",
            FinalPhase = "Executor"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("test command");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting agent execution")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrchestratorFails_LogsWarning()
    {
        const string testCorrelationId = "RUN-1234567890-1234";
        _correlationIdProviderMock.Setup(x => x.Generate()).Returns(testCorrelationId);

        var expectedResult = new OrchestratorResult
        {
            IsSuccess = false,
            RunId = "run-001",
            FinalPhase = "Failed"
        };

        _orchestratorMock.Setup(x => x.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        await _sut.ExecuteAsync("test command");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Agent execution failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

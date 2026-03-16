using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.Execution.SubagentRuns;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Agents.Persistence.Runs;
using nirmata.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.SubagentRuns;

/// <summary>
/// Integration tests for subagent orchestrator with tool calling (Task 6.5).
/// </summary>
public class SubagentOrchestratorToolCallingTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IToolCallingLoop> _toolCallingLoopMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<ILogger<SubagentOrchestrator>> _loggerMock;
    private readonly SubagentOrchestrator _sut;

    public SubagentOrchestratorToolCallingTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _toolCallingLoopMock = new Mock<IToolCallingLoop>();
        _workspaceMock = new Mock<IWorkspace>();
        _loggerMock = new Mock<ILogger<SubagentOrchestrator>>();

        _workspaceMock.Setup(w => w.AosRootPath).Returns(Path.GetTempPath());

        SetupRunLifecycleManager("RUN-001");

        _sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RunSubagentAsync_InvokesToolCallingLoop_WithCorrectRequest()
    {
        // Arrange
        var request = CreateRequest();
        ToolCallingRequest? capturedRequest = null;

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateSuccessResult());

        // Act
        await _sut.RunSubagentAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.CorrelationId.Should().Be(request.CorrelationId);
        capturedRequest.Messages.Should().HaveCountGreaterThanOrEqualTo(2);
        capturedRequest.Messages[0].Role.Should().Be(ToolCallingRole.System);
        capturedRequest.Messages[1].Role.Should().Be(ToolCallingRole.User);
    }

    [Fact]
    public async Task RunSubagentAsync_MapsBudgetToToolCallingOptions()
    {
        // Arrange
        var request = CreateRequest(budget: new SubagentBudget
        {
            MaxIterations = 25,
            MaxToolCalls = 50,
            MaxExecutionTimeSeconds = 120,
            MaxTokens = 5000
        });
        ToolCallingRequest? capturedRequest = null;

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateSuccessResult());

        // Act
        await _sut.RunSubagentAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Options.MaxIterations.Should().Be(25);
        capturedRequest.Options.Timeout.Should().Be(TimeSpan.FromSeconds(120));
        capturedRequest.Options.MaxTotalTokens.Should().Be(5000);
        capturedRequest.Options.EnableParallelToolExecution.Should().BeTrue();
        capturedRequest.Options.MaxParallelToolExecutions.Should().Be(32);
    }

    [Fact]
    public async Task RunSubagentAsync_IncludesContextDataInRequest()
    {
        // Arrange
        var request = CreateRequest();
        request.ContextData["key1"] = "value1";
        request.ContextData["key2"] = 42;

        ToolCallingRequest? capturedRequest = null;

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateSuccessResult());

        // Act
        await _sut.RunSubagentAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Context.Should().ContainKey("runId");
        capturedRequest.Context.Should().ContainKey("taskId");
        capturedRequest.Context["taskId"].Should().Be(request.TaskId);
    }

    [Fact]
    public async Task RunSubagentAsync_ReturnsSuccess_WhenLoopCompletesNaturally()
    {
        // Arrange
        var request = CreateRequest();
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult());

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task RunSubagentAsync_ReturnsFailure_WhenLoopHitsMaxIterations()
    {
        // Arrange
        var request = CreateRequest();
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Max iterations reached"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 10,
                CompletionReason = ToolCallingCompletionReason.MaxIterationsReached,
                Error = new ToolCallingError
                {
                    Code = "MaxIterationsReached",
                    Message = "Maximum iterations (10) reached"
                },
                Usage = new ToolCallingUsageStats
                {
                    TotalPromptTokens = 500,
                    TotalCompletionTokens = 200,
                    IterationCount = 10
                }
            });

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Maximum iterations");
        result.Metrics.IterationCount.Should().Be(10);
    }

    [Fact]
    public async Task RunSubagentAsync_ReturnsFailure_WhenLoopTimesOut()
    {
        // Arrange
        var request = CreateRequest();
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Timeout"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 3,
                CompletionReason = ToolCallingCompletionReason.Timeout,
                Error = new ToolCallingError
                {
                    Code = "Timeout",
                    Message = "Execution timed out"
                },
                Usage = new ToolCallingUsageStats
                {
                    TotalPromptTokens = 300,
                    TotalCompletionTokens = 100,
                    IterationCount = 3
                }
            });

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task RunSubagentAsync_ReturnsFailure_WhenLoopErrors()
    {
        // Arrange
        var request = CreateRequest();
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Error occurred"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 2,
                CompletionReason = ToolCallingCompletionReason.Error,
                Error = new ToolCallingError
                {
                    Code = "LlmProviderError",
                    Message = "LLM provider returned error"
                },
                Usage = new ToolCallingUsageStats
                {
                    TotalPromptTokens = 200,
                    TotalCompletionTokens = 50,
                    IterationCount = 2
                }
            });

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("error");
    }

    [Fact]
    public async Task RunSubagentAsync_PopulatesMetrics_FromLoopResult()
    {
        // Arrange
        var request = CreateRequest();
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Done"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 5,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats
                {
                    TotalPromptTokens = 1000,
                    TotalCompletionTokens = 500,
                    IterationCount = 5
                }
            });

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Metrics.IterationCount.Should().Be(5);
        result.Metrics.TokensConsumed.Should().Be(1500);
    }

    [Fact]
    public async Task RunSubagentAsync_HandlesLoopException_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Loop crashed"));

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Loop crashed");
    }

    [Fact]
    public async Task RunSubagentAsync_PropagatesCancellation()
    {
        // Arrange
        var request = CreateRequest();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.RunSubagentAsync(request, cts.Token));
    }

    private SubagentRunRequest CreateRequest(
        string taskId = "TSK-001",
        string subagentConfig = "task_executor",
        SubagentBudget? budget = null)
    {
        return new SubagentRunRequest
        {
            RunId = "RUN-TEST",
            TaskId = taskId,
            SubagentConfig = subagentConfig,
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            CorrelationId = "corr-123",
            Budget = budget ?? new SubagentBudget()
        };
    }

    private ToolCallingResult CreateSuccessResult()
    {
        return new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Task completed successfully"),
            ConversationHistory = new List<ToolCallingMessage>
            {
                ToolCallingMessage.System("You are a subagent"),
                ToolCallingMessage.User("Execute the assigned task using available tools."),
                ToolCallingMessage.Assistant("Task completed successfully")
            },
            IterationCount = 1,
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            Usage = new ToolCallingUsageStats
            {
                TotalPromptTokens = 100,
                TotalCompletionTokens = 50,
                IterationCount = 1
            }
        };
    }

    private void SetupRunLifecycleManager(string runId)
    {
        var context = new nirmata.Agents.Models.Runtime.RunContext { RunId = runId, StartedAt = DateTimeOffset.UtcNow };
        _runLifecycleManagerMock
            .Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        _runLifecycleManagerMock
            .Setup(x => x.RecordCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}

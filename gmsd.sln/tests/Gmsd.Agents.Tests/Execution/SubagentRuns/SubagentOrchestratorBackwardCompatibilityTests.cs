using FluentAssertions;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.SubagentRuns;

/// <summary>
/// Backward compatibility tests for subagent orchestrator with tool calling protocol (Task 9.5).
/// Verifies that existing subagent workflows continue to work with the new tool calling implementation.
/// </summary>
public class SubagentOrchestratorBackwardCompatibilityTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IToolCallingLoop> _toolCallingLoopMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<ILogger<SubagentOrchestrator>> _loggerMock;
    private readonly SubagentOrchestrator _sut;

    public SubagentOrchestratorBackwardCompatibilityTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _toolCallingLoopMock = new Mock<IToolCallingLoop>();
        _workspaceMock = new Mock<IWorkspace>();
        _loggerMock = new Mock<ILogger<SubagentOrchestrator>>();

        _workspaceMock.Setup(w => w.AosRootPath).Returns(Path.GetTempPath());

        // Setup default run lifecycle behavior
        var context = new Gmsd.Agents.Models.Runtime.RunContext { RunId = "RUN-BC-001", StartedAt = DateTimeOffset.UtcNow };
        _runLifecycleManagerMock
            .Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        _runLifecycleManagerMock
            .Setup(x => x.RecordCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Test data for legacy file scope tests.
    /// </summary>
    public static TheoryData<string[]> LegacyFileScopeData => new()
    {
        { new[] { "src/" } },
        { new[] { "src/", "tests/" } },
        { new[] { "docs/", "examples/", "scripts/" } }
    };

    /// <summary>
    /// Verifies that existing subagent workflows without tool calling still work
    /// (backward compatibility with pre-tool-calling subagent behavior).
    /// </summary>
    [Fact]
    public async Task RunSubagentAsync_WithoutToolCalling_StillCompletesSuccessfully()
    {
        // Arrange - Request with no tools and simple completion
        var request = new SubagentRunRequest
        {
            RunId = "RUN-LEGACY",
            TaskId = "TSK-LEGACY-001",
            SubagentConfig = "simple_task",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            CorrelationId = "corr-legacy",
            Budget = new SubagentBudget()
        };

        // Mock loop to return simple completion without any tool calls
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Task completed without tools"),
                ConversationHistory = new List<ToolCallingMessage>
                {
                    ToolCallingMessage.System("You are a subagent"),
                    ToolCallingMessage.User("Execute task"),
                    ToolCallingMessage.Assistant("Task completed without tools")
                },
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats
                {
                    TotalPromptTokens = 50,
                    TotalCompletionTokens = 25,
                    IterationCount = 1
                }
            });

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.RunId.Should().Be("RUN-BC-001");
        result.TaskId.Should().Be("TSK-LEGACY-001");
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.Metrics.IterationCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that existing subagent configuration formats still work
    /// (backward compatibility with existing config strings).
    /// </summary>
    [Theory]
    [InlineData("task_executor")]
    [InlineData("code_review")]
    [InlineData("documentation_generator")]
    [InlineData("simple_agent")]
    public async Task RunSubagentAsync_WithExistingConfigs_WorksWithAll(string configName)
    {
        // Arrange
        var request = new SubagentRunRequest
        {
            RunId = "RUN-CONFIG-TEST",
            TaskId = $"TSK-{configName}",
            SubagentConfig = configName,
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            Budget = new SubagentBudget()
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult());

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that existing budget configurations still work correctly
    /// (backward compatibility with SubagentBudget structure).
    /// </summary>
    [Fact]
    public async Task RunSubagentAsync_WithLegacyBudgetConfiguration_MapsCorrectly()
    {
        // Arrange - Budget with only basic settings (as used by existing workflows)
        var request = new SubagentRunRequest
        {
            RunId = "RUN-BUDGET-TEST",
            TaskId = "TSK-BUDGET",
            SubagentConfig = "budget_test",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            Budget = new SubagentBudget
            {
                MaxIterations = 5,
                MaxToolCalls = 10,
                MaxExecutionTimeSeconds = 60,
                MaxTokens = 1000
            }
        };

        ToolCallingRequest? capturedRequest = null;
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateSuccessResult());

        // Act
        await _sut.RunSubagentAsync(request);

        // Assert - Verify budget is correctly mapped to ToolCallingOptions
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Options.MaxIterations.Should().Be(5);
        capturedRequest.Options.Timeout.Should().Be(TimeSpan.FromSeconds(60));
        capturedRequest.Options.MaxTotalTokens.Should().Be(1000);
        capturedRequest.Options.EnableParallelToolExecution.Should().BeTrue();
        capturedRequest.Options.MaxParallelToolExecutions.Should().Be(32);
    }

    /// <summary>
    /// Verifies that existing workflows with ContextData still work
    /// (backward compatibility with context passing).
    /// </summary>
    [Fact]
    public async Task RunSubagentAsync_WithLegacyContextData_PassesToLoop()
    {
        // Arrange - ContextData as used by existing workflows
        var request = new SubagentRunRequest
        {
            RunId = "RUN-CTX-TEST",
            TaskId = "TSK-CTX",
            SubagentConfig = "context_test",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            Budget = new SubagentBudget(),
            ContextData =
            {
                ["legacy_key_1"] = "legacy_value_1",
                ["legacy_key_2"] = 42,
                ["legacy_key_3"] = true
            }
        };

        ToolCallingRequest? capturedRequest = null;
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateSuccessResult());

        // Act
        await _sut.RunSubagentAsync(request);

        // Assert - Context data should be present in messages
        capturedRequest.Should().NotBeNull();
        var userMessages = capturedRequest!.Messages.Where(m => m.Role == ToolCallingRole.User).ToList();
        userMessages.Should().Contain(m => m.Content!.Contains("legacy_key_1"));
        userMessages.Should().Contain(m => m.Content!.Contains("legacy_value_1"));
    }

    /// <summary>
    /// Verifies that existing file scope restrictions still work
    /// (backward compatibility with AllowedFileScope).
    /// </summary>
    [Theory]
    [MemberData(nameof(LegacyFileScopeData))]
    public async Task RunSubagentAsync_WithLegacyFileScopes_WorksCorrectly(string[] scopes)
    {
        // Arrange
        var request = new SubagentRunRequest
        {
            RunId = "RUN-SCOPE-TEST",
            TaskId = "TSK-SCOPE",
            SubagentConfig = "scope_test",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = scopes.ToList().AsReadOnly(),
            Budget = new SubagentBudget()
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult());

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that parent-child run relationships still work
    /// (backward compatibility with ParentRunId tracking).
    /// </summary>
    [Fact]
    public async Task RunSubagentAsync_WithParentRunId_MaintainsHierarchy()
    {
        // Arrange
        var request = new SubagentRunRequest
        {
            RunId = "RUN-CHILD",
            TaskId = "TSK-CHILD",
            SubagentConfig = "child_test",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            ParentRunId = "RUN-PARENT-001", // Existing parent tracking
            Budget = new SubagentBudget()
        };

        ToolCallingRequest? capturedRequest = null;
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateSuccessResult());

        // Act
        await _sut.RunSubagentAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Context.Should().ContainKey("parentRunId");
        capturedRequest.Context["parentRunId"].Should().Be("RUN-PARENT-001");
    }

    /// <summary>
    /// Verifies that empty/minimal requests still work
    /// (backward compatibility with minimal configuration).
    /// </summary>
    [Fact]
    public async Task RunSubagentAsync_WithMinimalConfiguration_Works()
    {
        // Arrange - Minimal request with only required fields
        var request = new SubagentRunRequest
        {
            RunId = "RUN-MINIMAL",
            TaskId = "TSK-MINIMAL",
            SubagentConfig = "minimal",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "." }.ToList().AsReadOnly()
            // Budget and other fields use defaults
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResult());

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.TaskId.Should().Be("TSK-MINIMAL");
    }

    /// <summary>
    /// Verifies that result structure maintains backward compatibility
    /// (existing result fields are populated correctly).
    /// </summary>
    [Fact]
    public async Task RunSubagentAsync_ResultStructure_MaintainsBackwardCompatibility()
    {
        // Arrange
        var request = new SubagentRunRequest
        {
            RunId = "RUN-RESULT-TEST",
            TaskId = "TSK-RESULT",
            SubagentConfig = "result_test",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            Budget = new SubagentBudget()
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Completed"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 3,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats
                {
                    TotalPromptTokens = 300,
                    TotalCompletionTokens = 150,
                    IterationCount = 3
                }
            });

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert - All expected fields are present and populated
        result.Success.Should().BeTrue();
        result.RunId.Should().NotBeNullOrEmpty();
        result.TaskId.Should().Be("TSK-RESULT");
        result.Metrics.Should().NotBeNull();
        result.Metrics.IterationCount.Should().Be(3);
        result.Metrics.TokensConsumed.Should().Be(450);
        result.NormalizedOutput.Should().NotBeNullOrEmpty();
        result.DeterministicHash.Should().NotBeNullOrEmpty();
        result.ModifiedFiles.Should().NotBeNull();
        result.ToolCalls.Should().NotBeNull();
        result.EvidenceArtifacts.Should().NotBeNull();
        result.ResultData.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies error handling maintains backward compatibility
    /// (errors are reported in expected format).
    /// </summary>
    [Fact]
    public async Task RunSubagentAsync_WhenLoopFails_ErrorFormatIsCompatible()
    {
        // Arrange
        var request = new SubagentRunRequest
        {
            RunId = "RUN-ERROR-TEST",
            TaskId = "TSK-ERROR",
            SubagentConfig = "error_test",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            Budget = new SubagentBudget()
        };

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
                    TotalPromptTokens = 100,
                    TotalCompletionTokens = 50,
                    IterationCount = 2
                }
            });

        // Act
        var result = await _sut.RunSubagentAsync(request);

        // Assert - Error format is as expected by existing consumers
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Metrics.IterationCount.Should().Be(2);
        result.ResultData.Should().ContainKey("error");
    }

    /// <summary>
    /// Verifies cancellation behavior maintains backward compatibility.
    /// </summary>
    [Fact]
    public async Task RunSubagentAsync_WhenCancelled_PropagatesCorrectly()
    {
        // Arrange
        var request = new SubagentRunRequest
        {
            RunId = "RUN-CANCEL-TEST",
            TaskId = "TSK-CANCEL",
            SubagentConfig = "cancel_test",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            Budget = new SubagentBudget()
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.RunSubagentAsync(request, cts.Token));
    }

    private static ToolCallingResult CreateSuccessResult()
    {
        return new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Success"),
            ConversationHistory = new List<ToolCallingMessage>(),
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
}

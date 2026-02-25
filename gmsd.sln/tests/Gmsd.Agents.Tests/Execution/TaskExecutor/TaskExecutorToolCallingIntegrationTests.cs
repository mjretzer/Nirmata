using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Agents.Execution.Execution.TaskExecutor;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.TaskExecutor;

/// <summary>
/// Integration tests for TaskExecutor with tool calling loop (Task 7.4).
/// </summary>
public class TaskExecutorToolCallingIntegrationTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IToolCallingLoop> _toolCallingLoopMock;
    private readonly Mock<IToolRegistry> _toolRegistryMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<ILogger<Gmsd.Agents.Execution.Execution.TaskExecutor.TaskExecutor>> _loggerMock;
    private readonly Gmsd.Agents.Execution.Execution.TaskExecutor.TaskExecutor _sut;

    public TaskExecutorToolCallingIntegrationTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _toolCallingLoopMock = new Mock<IToolCallingLoop>();
        _toolRegistryMock = new Mock<IToolRegistry>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _loggerMock = new Mock<ILogger<Gmsd.Agents.Execution.Execution.TaskExecutor.TaskExecutor>>();

        _workspaceMock.Setup(w => w.AosRootPath).Returns(Path.GetTempPath());

        _sut = new Gmsd.Agents.Execution.Execution.TaskExecutor.TaskExecutor(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulToolCalls_ReturnsSuccessWithModifiedFiles()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var plan = CreateSamplePlan();
        WritePlanFile(tempDir.Path, plan);

        var modifiedFiles = new[] { "src/file1.cs", "src/file2.cs" };
        SetupToolCallingRunWithModifiedFiles(modifiedFiles);
        SetupRunLifecycleManager();

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ModifiedFiles.Should().Contain(modifiedFiles);
        result.DeterministicHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithToolCallFailure_ReturnsFailureWithError()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var plan = CreateSamplePlan();
        WritePlanFile(tempDir.Path, plan);

        SetupToolCallingRunWithError("ToolExecutionError", "Failed to execute write_file tool");
        SetupRunLifecycleManager();

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to execute write_file tool");
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxIterationsReached_ReturnsFailure()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var plan = CreateSamplePlan();
        WritePlanFile(tempDir.Path, plan);

        SetupToolCallingRunWithCompletionReason(
            ToolCallingCompletionReason.MaxIterationsReached,
            "Maximum iterations (20) reached");
        SetupRunLifecycleManager();

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Maximum iterations");
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ReturnsFailure()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var plan = CreateSamplePlan();
        WritePlanFile(tempDir.Path, plan);

        SetupToolCallingRunWithCompletionReason(
            ToolCallingCompletionReason.Timeout,
            "Tool calling loop exceeded timeout");
        SetupRunLifecycleManager();

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timeout");
    }

    [Fact]
    public async Task ExecuteAsync_ExtractsModifiedFilesFromToolResults()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var plan = CreateSamplePlan();
        WritePlanFile(tempDir.Path, plan);

        var conversationHistory = new List<ToolCallingMessage>
        {
            ToolCallingMessage.System("Execute task"),
            ToolCallingMessage.User("Create a file"),
            ToolCallingMessage.Assistant(null, new[]
            {
                new ToolCallingRequestItem
                {
                    Id = "call-1",
                    Name = "write_file",
                    ArgumentsJson = "{\"path\": \"src/newfile.cs\", \"content\": \"test\"}"
                }
            }),
            ToolCallingMessage.Tool("call-1", "write_file", "{\"success\": true, \"modifiedFile\": \"src/newfile.cs\"}")
        };

        SetupToolCallingRunWithHistory(conversationHistory);
        SetupRunLifecycleManager();

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ModifiedFiles.Should().Contain("src/newfile.cs");
    }

    [Fact]
    public async Task ExecuteAsync_MapsFileScopesToToolCallingContext()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var plan = CreateSamplePlan();
        WritePlanFile(tempDir.Path, plan);

        ToolCallingRequest? capturedRequest = null;
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(CreateSuccessfulResult());

        SetupRunLifecycleManager();

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/", "tests/" },
            ParentRunId = "PARENT-RUN-001",
            CorrelationId = "CORR-001"
        };

        // Act
        await _sut.ExecuteAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Context.Should().ContainKey("allowedFileScope");
        capturedRequest.Context["allowedFileScope"].Should().BeEquivalentTo(new[] { "src/", "tests/" });
        capturedRequest.Context.Should().ContainKey("taskId");
        capturedRequest.Context["taskId"].Should().Be("TSK-001");
        capturedRequest.CorrelationId.Should().Be("CORR-001");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancelledLoop_ReturnsFailure()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var plan = CreateSamplePlan();
        WritePlanFile(tempDir.Path, plan);

        SetupToolCallingRunWithCompletionReason(
            ToolCallingCompletionReason.Cancelled,
            "Tool calling loop was cancelled");
        SetupRunLifecycleManager();

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_WithLoopException_ReturnsFailure()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var plan = CreateSamplePlan();
        WritePlanFile(tempDir.Path, plan);

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tool registry unavailable"));

        SetupRunLifecycleManager();

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }
        };

        // Act
        var result = await _sut.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Tool registry unavailable");
    }

    private static TaskPlanModel CreateSamplePlan()
    {
        return new TaskPlanModel
        {
            TaskId = "TSK-001",
            Title = "Sample Plan",
            Description = "Test plan for integration testing",
            Steps = new List<PlanStep>
            {
                new() { StepId = "step-1", StepType = "write_file", TargetPath = "src/file1.cs", Description = "Create file 1" },
                new() { StepId = "step-2", StepType = "write_file", TargetPath = "src/file2.cs", Description = "Create file 2" }
            },
            FileScopes = new List<FileScopeEntry>
            {
                new() { Path = "src/", ScopeType = "write" }
            }
        };
    }

    private static void WritePlanFile(string directory, TaskPlanModel plan)
    {
        var planPath = Path.Combine(directory, "plan.json");
        var normalizedPlan = new TaskPlanModel
        {
            SchemaVersion = plan.SchemaVersion,
            TaskId = string.IsNullOrWhiteSpace(plan.TaskId) ? "TSK-001" : plan.TaskId,
            Title = string.IsNullOrWhiteSpace(plan.Title) ? "Sample Plan" : plan.Title,
            Description = string.IsNullOrWhiteSpace(plan.Description) ? "Test plan for integration testing" : plan.Description,
            FileScopes = plan.FileScopes.Select(scope => new FileScopeEntry
            {
                Path = scope.Path,
                ScopeType = scope.ScopeType,
                Description = scope.Description ?? string.Empty
            }).ToList(),
            Steps = plan.Steps.Select(step => new PlanStep
            {
                StepId = step.StepId,
                StepType = step.StepType,
                TargetPath = step.TargetPath ?? string.Empty,
                Description = step.Description
            }).ToList(),
            VerificationSteps = plan.VerificationSteps
        };

        var json = JsonSerializer.Serialize(normalizedPlan, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(planPath, json);
    }

    private void SetupRunLifecycleManager(string runId = "RUN-001")
    {
        var context = new Gmsd.Agents.Models.Runtime.RunContext { RunId = runId, StartedAt = DateTimeOffset.UtcNow };
        _runLifecycleManagerMock
            .Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupToolCallingRunWithModifiedFiles(string[] modifiedFiles)
    {
        var history = modifiedFiles.Select(f =>
            ToolCallingMessage.Tool(Guid.NewGuid().ToString("N"), "write_file", $"{{\"success\": true, \"modifiedFile\": \"{f}\"}}")).ToList();

        var result = new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Task execution completed"),
            ConversationHistory = history,
            IterationCount = 1,
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            Usage = new ToolCallingUsageStats
            {
                TotalPromptTokens = 100,
                TotalCompletionTokens = 50,
                IterationCount = 1
            }
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupToolCallingRunWithHistory(List<ToolCallingMessage> history)
    {
        var result = new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Task execution completed"),
            ConversationHistory = history,
            IterationCount = history.Count(h => h.Role == ToolCallingRole.Assistant),
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            Usage = new ToolCallingUsageStats
            {
                TotalPromptTokens = 100 * history.Count,
                TotalCompletionTokens = 50,
                IterationCount = 1
            }
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupToolCallingRunWithError(string errorCode, string errorMessage)
    {
        var result = new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Execution failed"),
            ConversationHistory = new List<ToolCallingMessage>(),
            IterationCount = 1,
            CompletionReason = ToolCallingCompletionReason.Error,
            Error = new ToolCallingError
            {
                Code = errorCode,
                Message = errorMessage
            },
            Usage = new ToolCallingUsageStats
            {
                TotalPromptTokens = 50,
                TotalCompletionTokens = 25,
                IterationCount = 1
            }
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupToolCallingRunWithCompletionReason(ToolCallingCompletionReason reason, string errorMessage)
    {
        var result = new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Execution result"),
            ConversationHistory = new List<ToolCallingMessage>(),
            IterationCount = 1,
            CompletionReason = reason,
            Error = reason != ToolCallingCompletionReason.CompletedNaturally
                ? new ToolCallingError { Code = reason.ToString(), Message = errorMessage }
                : null,
            Usage = new ToolCallingUsageStats
            {
                TotalPromptTokens = 50,
                TotalCompletionTokens = 25,
                IterationCount = 1
            }
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private static ToolCallingResult CreateSuccessfulResult()
    {
        return new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Task completed"),
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

public class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}

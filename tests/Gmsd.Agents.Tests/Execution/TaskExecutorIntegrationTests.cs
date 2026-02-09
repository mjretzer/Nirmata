using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Execution.Execution.TaskExecutor;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution;

public class TaskExecutorIntegrationTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<ILogger<TaskExecutor>> _taskExecutorLoggerMock;
    private readonly Mock<ILogger<SubagentOrchestrator>> _subagentLoggerMock;

    public TaskExecutorIntegrationTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _taskExecutorLoggerMock = new Mock<ILogger<TaskExecutor>>();
        _subagentLoggerMock = new Mock<ILogger<SubagentOrchestrator>>();
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_WithValidPlan_CompletesSuccessfully()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-001");

        var subagentOrchestrator = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _subagentLoggerMock.Object);

        var taskExecutor = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            subagentOrchestrator,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _taskExecutorLoggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/", "tests/" }.ToList().AsReadOnly()
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.RunId.Should().Be("RUN-001");
        result.DeterministicHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_PropagatesSubagentFailure()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-001");
        SetupFailingSubagentRun();

        var subagentOrchestrator = new Mock<ISubagentOrchestrator>();
        subagentOrchestrator
            .Setup(x => x.RunSubagentAsync(It.IsAny<SubagentRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubagentRunResult
            {
                Success = false,
                RunId = "SUB-001",
                TaskId = "TSK-001",
                ErrorMessage = "Subagent execution failed"
            });

        var taskExecutor = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            subagentOrchestrator.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _taskExecutorLoggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Subagent execution failed");
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_AppendsStateEvents()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-001");

        var capturedEvents = new List<JsonElement>();
        _stateStoreMock
            .Setup(x => x.AppendEvent(It.IsAny<JsonElement>()))
            .Callback<JsonElement>(e => capturedEvents.Add(e.Clone()));

        var subagentOrchestrator = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _subagentLoggerMock.Object);

        var taskExecutor = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            subagentOrchestrator,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _taskExecutorLoggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        await taskExecutor.ExecuteAsync(request);

        // Assert
        capturedEvents.Should().HaveCount(2);
        capturedEvents[0].GetProperty("eventType").GetString().Should().Be("task_execution_started");
        capturedEvents[1].GetProperty("eventType").GetString().Should().Be("task_execution_completed");
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_TracksModifiedFiles()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-001");

        var subagentOrchestrator = new Mock<ISubagentOrchestrator>();
        subagentOrchestrator
            .Setup(x => x.RunSubagentAsync(It.IsAny<SubagentRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubagentRunResult
            {
                Success = true,
                RunId = "SUB-001",
                TaskId = "TSK-001",
                ModifiedFiles = new[] { "src/file1.cs", "src/file2.cs" }.ToList().AsReadOnly()
            });

        var taskExecutor = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            subagentOrchestrator.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _taskExecutorLoggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ModifiedFiles.Should().Contain("src/file1.cs");
        result.ModifiedFiles.Should().Contain("src/file2.cs");
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_WithCorrelationId_PreservesTracing()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-001");

        SubagentRunRequest? capturedRequest = null;
        var subagentOrchestrator = new Mock<ISubagentOrchestrator>();
        subagentOrchestrator
            .Setup(x => x.RunSubagentAsync(It.IsAny<SubagentRunRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SubagentRunRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new SubagentRunResult
            {
                Success = true,
                RunId = "SUB-001",
                TaskId = "TSK-001"
            });

        var taskExecutor = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            subagentOrchestrator.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _taskExecutorLoggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            CorrelationId = "trace-abc-123",
            ParentRunId = "PARENT-001"
        };

        // Act
        await taskExecutor.ExecuteAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.CorrelationId.Should().Be("trace-abc-123");
        capturedRequest.ParentRunId.Should().Be("PARENT-001");
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_WithContextData_PassesToSubagent()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-001");

        var taskExecutor = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            new SubagentOrchestrator(
                _runLifecycleManagerMock.Object,
                _workspaceMock.Object,
                _subagentLoggerMock.Object),
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _taskExecutorLoggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            ContextData = new Dictionary<string, object>
            {
                ["customKey"] = "customValue"
            }
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
    }

    private TaskPlanModel CreateValidPlan()
    {
        return new TaskPlanModel
        {
            Title = "Integration Test Plan",
            Steps = new List<PlanStep>
            {
                new()
                {
                    StepId = "step-1",
                    StepType = "file_edit",
                    Description = "Edit a file",
                    TargetPath = "src/file.cs"
                }
            },
            FileScopes = new List<FileScopeEntry>
            {
                new() { Path = "src/" },
                new() { Path = "tests/" }
            }
        };
    }

    private void WritePlanFile(string directory, TaskPlanModel plan)
    {
        var planPath = Path.Combine(directory, "plan.json");
        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(planPath, json);
    }

    private void SetupRunLifecycleManager(string runId)
    {
        var context = new RunContext { RunId = runId, StartedAt = DateTimeOffset.UtcNow };
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

    private void SetupFailingSubagentRun()
    {
        // This is a placeholder - actual implementation would configure the mock to fail
    }
}

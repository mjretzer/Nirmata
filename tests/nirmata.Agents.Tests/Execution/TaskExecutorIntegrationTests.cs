using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.Execution.SubagentRuns;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;
using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Persistence.Runs;
using nirmata.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TaskExecutorClass = nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutor;
using TaskPlanModel = nirmata.Agents.Execution.Execution.TaskExecutor.TaskPlanModel;
using PlanStep = nirmata.Agents.Execution.Execution.TaskExecutor.PlanStep;
using FileScopeEntry = nirmata.Agents.Execution.Execution.TaskExecutor.FileScopeEntry;
using TaskExecutionRequest = nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutionRequest;

namespace nirmata.Agents.Tests.Execution;

public class TaskExecutorIntegrationTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IToolCallingLoop> _toolCallingLoopMock;
    private readonly Mock<IToolRegistry> _toolRegistryMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<ILogger<TaskExecutorClass>> _taskExecutorLoggerMock;
    private readonly Mock<ILogger<SubagentOrchestrator>> _subagentLoggerMock;

    public TaskExecutorIntegrationTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _toolCallingLoopMock = new Mock<IToolCallingLoop>();
        _toolRegistryMock = new Mock<IToolRegistry>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _taskExecutorLoggerMock = new Mock<ILogger<TaskExecutorClass>>();
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

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Completed"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats { IterationCount = 1 }
            });

        var taskExecutor = new TaskExecutorClass(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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
        
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Failed"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.Error,
                Error = new ToolCallingError { Code = "SubagentFailed", Message = "Subagent execution failed" },
                Usage = new ToolCallingUsageStats { IterationCount = 1 }
            });

        var taskExecutor = new TaskExecutorClass(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Completed"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats { IterationCount = 1 }
            });

        var taskExecutor = new TaskExecutorClass(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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

        var history = new List<ToolCallingMessage>
        {
            ToolCallingMessage.Tool("call-1", "write_file", "{\"success\": true, \"modifiedFile\": \"src/file1.cs\"}"),
            ToolCallingMessage.Tool("call-2", "write_file", "{\"success\": true, \"modifiedFile\": \"src/file2.cs\"}")
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                ConversationHistory = history,
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats { IterationCount = 1 },
                FinalMessage = ToolCallingMessage.Assistant("Task completed")
            });

        var taskExecutor = new TaskExecutorClass(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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

        ToolCallingRequest? capturedRequest = null;
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Completed"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats { IterationCount = 1 }
            });

        var taskExecutor = new TaskExecutorClass(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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
        capturedRequest.Context["parentRunId"].Should().Be("PARENT-001");
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

        ToolCallingRequest? capturedRequest = null;
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolCallingRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Completed"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats { IterationCount = 1 }
            });

        var taskExecutor = new TaskExecutorClass(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Context["customKey"].Should().Be("customValue");
    }

    private TaskPlanModel CreateValidPlan()
    {
        return new TaskPlanModel
        {
            TaskId = "TSK-001",
            Title = "Integration Test Plan",
            Description = "Integration test description",
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
                new() { Path = "src/" }
            }
        };
    }

    private void WritePlanFile(string directory, TaskPlanModel plan)
    {
        var planPath = Path.Combine(directory, "plan.json");
        var normalizedPlan = new TaskPlanModel
        {
            SchemaVersion = plan.SchemaVersion,
            TaskId = string.IsNullOrWhiteSpace(plan.TaskId) ? "TSK-001" : plan.TaskId,
            Title = string.IsNullOrWhiteSpace(plan.Title) ? "Integration Test Plan" : plan.Title,
            Description = string.IsNullOrWhiteSpace(plan.Description) ? "Integration test description" : plan.Description,
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

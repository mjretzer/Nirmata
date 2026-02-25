using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TaskExecutorClass = Gmsd.Agents.Execution.Execution.TaskExecutor.TaskExecutor;
using TaskPlanModel = Gmsd.Agents.Execution.Execution.TaskExecutor.TaskPlanModel;
using PlanStep = Gmsd.Agents.Execution.Execution.TaskExecutor.PlanStep;
using FileScopeEntry = Gmsd.Agents.Execution.Execution.TaskExecutor.FileScopeEntry;
using TaskExecutionRequest = Gmsd.Agents.Execution.Execution.TaskExecutor.TaskExecutionRequest;

namespace Gmsd.Agents.Tests.Execution.TaskExecutor;

public class TaskExecutorEvidencePersistenceTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IToolCallingLoop> _toolCallingLoopMock;
    private readonly Mock<IToolRegistry> _toolRegistryMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<ILogger<TaskExecutorClass>> _taskExecutorLoggerMock;

    public TaskExecutorEvidencePersistenceTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _toolCallingLoopMock = new Mock<IToolCallingLoop>();
        _toolRegistryMock = new Mock<IToolRegistry>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _taskExecutorLoggerMock = new Mock<ILogger<TaskExecutorClass>>();
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_GeneratesToolCallsLog()
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
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        
        var toolCallsLogPath = Path.Combine(workspacePath, "evidence", "runs", "RUN-001", "artifacts", "tool-calls.ndjson");
        File.Exists(toolCallsLogPath).Should().BeTrue("tool-calls.ndjson should be created");
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_GeneratesExecutionSummary()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-002");

        var history = new List<ToolCallingMessage>
        {
            ToolCallingMessage.Tool("call-1", "write_file", "{\"success\": true, \"modifiedFile\": \"src/file1.cs\"}"),
            ToolCallingMessage.Tool("call-2", "write_file", "{\"success\": true, \"modifiedFile\": \"src/file2.cs\"}")
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Completed"),
                ConversationHistory = history,
                IterationCount = 2,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats { IterationCount = 2 }
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
            TaskId = "TSK-002",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        var summaryPath = Path.Combine(workspacePath, "evidence", "runs", "RUN-002", "artifacts", "execution-summary.json");
        File.Exists(summaryPath).Should().BeTrue("execution-summary.json should be created");

        var summaryJson = File.ReadAllText(summaryPath);
        var summary = JsonSerializer.Deserialize<JsonElement>(summaryJson);
        
        summary.GetProperty("taskId").GetString().Should().Be("TSK-002");
        summary.GetProperty("runId").GetString().Should().Be("RUN-002");
        summary.GetProperty("iterations").GetInt32().Should().Be(2);
        summary.GetProperty("toolCallsCount").GetInt32().Should().Be(2);
        summary.GetProperty("completionStatus").GetString().Should().Be("success");
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_GeneratesDeterministicHash()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-003");

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
            TaskId = "TSK-003",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.DeterministicHash.Should().NotBeNullOrEmpty("deterministic hash should be computed");

        var hashFilePath = Path.Combine(workspacePath, "evidence", "runs", "RUN-003", "artifacts", "deterministic-hash");
        File.Exists(hashFilePath).Should().BeTrue("deterministic-hash file should be created");

        var hashContent = File.ReadAllText(hashFilePath).Trim();
        hashContent.Should().Be(result.DeterministicHash, "hash file content should match result hash");
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_GeneratesPatch()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-004");

        var history = new List<ToolCallingMessage>
        {
            ToolCallingMessage.Tool("call-1", "write_file", "{\"success\": true, \"modifiedFile\": \"src/file1.cs\"}")
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Completed"),
                ConversationHistory = history,
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
            TaskId = "TSK-004",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        var patchPath = Path.Combine(workspacePath, "evidence", "runs", "RUN-004", "artifacts", "changes.patch");
        File.Exists(patchPath).Should().BeTrue("changes.patch should be created");
    }

    [Fact]
    public async Task EndToEnd_TaskExecution_AllEvidenceArtifactsPresent()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager("RUN-005");

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
            TaskId = "TSK-005",
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        var result = await taskExecutor.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        var artifactsDir = Path.Combine(workspacePath, "evidence", "runs", "RUN-005", "artifacts");
        Directory.Exists(artifactsDir).Should().BeTrue("artifacts directory should exist");

        var toolCallsLog = Path.Combine(artifactsDir, "tool-calls.ndjson");
        var executionSummary = Path.Combine(artifactsDir, "execution-summary.json");
        var changesPatch = Path.Combine(artifactsDir, "changes.patch");
        var deterministicHash = Path.Combine(artifactsDir, "deterministic-hash");

        File.Exists(toolCallsLog).Should().BeTrue("tool-calls.ndjson should exist");
        File.Exists(executionSummary).Should().BeTrue("execution-summary.json should exist");
        File.Exists(changesPatch).Should().BeTrue("changes.patch should exist");
        File.Exists(deterministicHash).Should().BeTrue("deterministic-hash should exist");
    }

    private TaskPlanModel CreateValidPlan()
    {
        return new TaskPlanModel
        {
            TaskId = "TSK-001",
            Title = "Evidence Persistence Test Plan",
            Description = "Test plan for evidence persistence",
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
            Title = string.IsNullOrWhiteSpace(plan.Title) ? "Test Plan" : plan.Title,
            Description = string.IsNullOrWhiteSpace(plan.Description) ? "Test description" : plan.Description,
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
}

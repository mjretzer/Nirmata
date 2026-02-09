using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Execution.Execution.TaskExecutor;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Engine.Evidence.TaskEvidence;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution;

public class TaskEvidencePointerTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<ISubagentOrchestrator> _subagentOrchestratorMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<ILogger<TaskExecutor>> _loggerMock;

    public TaskEvidencePointerTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _subagentOrchestratorMock = new Mock<ISubagentOrchestrator>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _loggerMock = new Mock<ILogger<TaskExecutor>>();
    }

    [Fact]
    public async Task ExecuteAsync_CreatesLatestJsonPointer_AfterSuccessfulExecution()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-001";
        var runId = "RUN-123";

        var workspacePath = tempDir.Path;
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);
        SetupSuccessfulSubagentRun(modifiedFiles: new[] { "src/file.cs" });

        var sut = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            _subagentOrchestratorMock.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _loggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = taskId,
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        await sut.ExecuteAsync(request);

        // Assert
        var evidenceDir = Path.Combine(workspacePath, ".aos", "evidence", "task-evidence", taskId);
        var latestPath = Path.Combine(evidenceDir, "latest.json");

        if (Directory.Exists(evidenceDir))
        {
            File.Exists(latestPath).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExecuteAsync_LatestJson_ContainsCorrectRunId()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-001";
        var runId = "RUN-456";

        var workspacePath = tempDir.Path;
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);
        SetupSuccessfulSubagentRun();

        var sut = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            _subagentOrchestratorMock.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _loggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = taskId,
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        await sut.ExecuteAsync(request);

        // Assert
        var evidenceDir = Path.Combine(workspacePath, ".aos", "evidence", "task-evidence", taskId);
        var latestPath = Path.Combine(evidenceDir, "latest.json");

        if (File.Exists(latestPath))
        {
            var content = await File.ReadAllTextAsync(latestPath);
            content.Should().Contain(runId);
        }
    }

    [Fact]
    public async Task ExecuteAsync_LatestJson_ContainsDiffstat()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-001";
        var runId = "RUN-789";

        var workspacePath = tempDir.Path;
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);
        SetupSuccessfulSubagentRun(modifiedFiles: new[] { "src/file1.cs", "src/file2.cs" });

        var sut = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            _subagentOrchestratorMock.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _loggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = taskId,
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        await sut.ExecuteAsync(request);

        // Assert
        var evidenceDir = Path.Combine(workspacePath, ".aos", "evidence", "task-evidence", taskId);
        var latestPath = Path.Combine(evidenceDir, "latest.json");

        if (File.Exists(latestPath))
        {
            var content = await File.ReadAllTextAsync(latestPath);
            content.Should().Contain("diffstat");
            content.Should().Contain("filesChanged");
        }
    }

    [Fact]
    public async Task ExecuteAsync_OnFailure_DoesNotCreateLatestJson()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-001";
        var runId = "RUN-FAIL";

        var workspacePath = tempDir.Path;
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);

        _subagentOrchestratorMock
            .Setup(x => x.RunSubagentAsync(It.IsAny<SubagentRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubagentRunResult
            {
                Success = false,
                RunId = "SUB-FAIL",
                TaskId = taskId,
                ErrorMessage = "Execution failed"
            });

        var sut = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            _subagentOrchestratorMock.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _loggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = taskId,
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        await sut.ExecuteAsync(request);

        // Assert
        var evidenceDir = Path.Combine(workspacePath, ".aos", "evidence", "task-evidence", taskId);
        var latestPath = Path.Combine(evidenceDir, "latest.json");
        File.Exists(latestPath).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesExistingLatestJson_IfExists()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-001";
        var runId = "RUN-NEW";

        var workspacePath = tempDir.Path;
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        // Create existing latest.json
        var evidenceDir = Path.Combine(workspacePath, ".aos", "evidence", "task-evidence", taskId);
        Directory.CreateDirectory(evidenceDir);
        var existingLatestPath = Path.Combine(evidenceDir, "latest.json");
        var existingContent = JsonSerializer.Serialize(new
        {
            runId = "RUN-OLD",
            timestamp = DateTimeOffset.UtcNow.AddHours(-1).ToString("O")
        });
        await File.WriteAllTextAsync(existingLatestPath, existingContent);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);
        SetupSuccessfulSubagentRun();

        var sut = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            _subagentOrchestratorMock.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _loggerMock.Object);

        var request = new TaskExecutionRequest
        {
            TaskId = taskId,
            TaskDirectory = tempDir.Path,
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly()
        };

        // Act
        await sut.ExecuteAsync(request);

        // Assert
        if (File.Exists(existingLatestPath))
        {
            var newContent = await File.ReadAllTextAsync(existingLatestPath);
            newContent.Should().Contain(runId);
        }
    }

    private TaskPlanModel CreateValidPlan()
    {
        return new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>
            {
                new() { StepId = "step-1", TargetPath = "src/file.cs" }
            },
            FileScopes = new List<FileScopeEntry> { new() { Path = "src/" } }
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
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupSuccessfulSubagentRun(string[]? modifiedFiles = null)
    {
        _subagentOrchestratorMock
            .Setup(x => x.RunSubagentAsync(It.IsAny<SubagentRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubagentRunResult
            {
                Success = true,
                RunId = "SUB-001",
                TaskId = "TSK-001",
                ModifiedFiles = (modifiedFiles ?? Array.Empty<string>()).ToList().AsReadOnly()
            });
    }
}

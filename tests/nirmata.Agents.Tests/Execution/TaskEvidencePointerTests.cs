using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Agents.Execution.Execution.TaskExecutor;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;
using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Persistence.Runs;
using nirmata.Aos.Engine.Evidence.TaskEvidence;
using nirmata.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution;

public class TaskEvidencePointerTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IToolCallingLoop> _toolCallingLoopMock;
    private readonly Mock<IToolRegistry> _toolRegistryMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<ILogger<nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutor>> _loggerMock;

    public TaskEvidencePointerTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _toolCallingLoopMock = new Mock<IToolCallingLoop>();
        _toolRegistryMock = new Mock<IToolRegistry>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _loggerMock = new Mock<ILogger<nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutor>>();
    }

    [Fact]
    public async Task ExecuteAsync_CreatesLatestJsonPointer_AfterSuccessfulExecution()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-000001";
        var runId = Guid.NewGuid().ToString("N");

        var workspacePath = tempDir.Path;
        var aosRootPath = Path.Combine(workspacePath, ".aos");
        _workspaceMock.Setup(w => w.AosRootPath).Returns(aosRootPath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);
        SetupSuccessfulToolCallingRun(modifiedFiles: new[] { "src/file.cs" });

        var sut = new nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutor(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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
        var evidenceDir = Path.Combine(aosRootPath, "evidence", "task-evidence", taskId);
        var latestPath = Path.Combine(evidenceDir, "latest.json");

        File.Exists(latestPath).Should().BeTrue("latest.json should be created");
    }

    [Fact]
    public async Task ExecuteAsync_LatestJson_ContainsCorrectRunId()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-000001";
        var runId = Guid.NewGuid().ToString("N");

        var workspacePath = tempDir.Path;
        var aosRootPath = Path.Combine(workspacePath, ".aos");
        _workspaceMock.Setup(w => w.AosRootPath).Returns(aosRootPath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);
        SetupSuccessfulToolCallingRun();

        var sut = new nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutor(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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
        var evidenceDir = Path.Combine(aosRootPath, "evidence", "task-evidence", taskId);
        var latestPath = Path.Combine(evidenceDir, "latest.json");

        File.Exists(latestPath).Should().BeTrue("latest.json should be created");
        var content = await File.ReadAllTextAsync(latestPath);
        content.Should().Contain(runId);
    }

    [Fact]
    public async Task ExecuteAsync_LatestJson_ContainsDiffstat()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-000001";
        var runId = Guid.NewGuid().ToString("N");

        var workspacePath = tempDir.Path;
        var aosRootPath = Path.Combine(workspacePath, ".aos");
        _workspaceMock.Setup(w => w.AosRootPath).Returns(aosRootPath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);
        SetupSuccessfulToolCallingRun(modifiedFiles: new[] { "src/file1.cs", "src/file2.cs" });

        var sut = new nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutor(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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
        var evidenceDir = Path.Combine(aosRootPath, "evidence", "task-evidence", taskId);
        var latestPath = Path.Combine(evidenceDir, "latest.json");

        File.Exists(latestPath).Should().BeTrue("latest.json should be created");
        var content = await File.ReadAllTextAsync(latestPath);
        content.Should().Contain("diffstat");
        content.Should().Contain("filesChanged");
    }

    [Fact]
    public async Task ExecuteAsync_OnFailure_DoesNotCreateLatestJson()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-000001";
        var runId = Guid.NewGuid().ToString("N");

        var workspacePath = tempDir.Path;
        var aosRootPath = Path.Combine(workspacePath, ".aos");
        _workspaceMock.Setup(w => w.AosRootPath).Returns(aosRootPath);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Execution failed"),
                ConversationHistory = new List<ToolCallingMessage>(),
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.Error,
                Error = new ToolCallingError { Code = "ExecutionFailed", Message = "Execution failed" }
            });

        var sut = new nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutor(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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
        var evidenceDir = Path.Combine(aosRootPath, "evidence", "task-evidence", taskId);
        var latestPath = Path.Combine(evidenceDir, "latest.json");
        File.Exists(latestPath).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesExistingLatestJson_IfExists()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var taskId = "TSK-000001";
        var runId = Guid.NewGuid().ToString("N");
        var oldRunId = Guid.NewGuid().ToString("N");

        var workspacePath = tempDir.Path;
        var aosRootPath = Path.Combine(workspacePath, ".aos");
        _workspaceMock.Setup(w => w.AosRootPath).Returns(aosRootPath);

        // Create existing latest.json
        var evidenceDir = Path.Combine(aosRootPath, "evidence", "task-evidence", taskId);
        Directory.CreateDirectory(evidenceDir);
        var existingLatestPath = Path.Combine(evidenceDir, "latest.json");
        var existingContent = JsonSerializer.Serialize(new
        {
            runId = oldRunId,
            timestamp = DateTimeOffset.UtcNow.AddHours(-1).ToString("O")
        });
        await File.WriteAllTextAsync(existingLatestPath, existingContent);

        var plan = CreateValidPlan();
        WritePlanFile(tempDir.Path, plan);

        SetupRunLifecycleManager(runId);
        SetupSuccessfulToolCallingRun();

        var sut = new nirmata.Agents.Execution.Execution.TaskExecutor.TaskExecutor(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
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
        var result = await sut.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue("Execution should succeed");
        
        // Verify no warnings were logged (e.g. from UpdateTaskEvidencePointer)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "No warnings should be logged during execution");

        File.Exists(existingLatestPath).Should().BeTrue("latest.json should exist");
        var newContent = await File.ReadAllTextAsync(existingLatestPath);
        newContent.Should().Contain(runId);
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

    private void SetupSuccessfulToolCallingRun(string[]? modifiedFiles = null)
    {
        var history = modifiedFiles?.Select(f =>
            ToolCallingMessage.Tool(Guid.NewGuid().ToString("N"), "write_file", $"{{\"success\": true, \"modifiedFile\": \"{f}\"}}")).ToList()
            ?? new List<ToolCallingMessage>();

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                ConversationHistory = history,
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats { TotalPromptTokens = 100, TotalCompletionTokens = 50, IterationCount = 1 },
                FinalMessage = ToolCallingMessage.Assistant("Task completed")
            });
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

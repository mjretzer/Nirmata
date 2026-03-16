using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Agents.Execution.Execution.TaskExecutor;
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

public class TaskExecutorScopeEnforcementTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IToolCallingLoop> _toolCallingLoopMock;
    private readonly Mock<IToolRegistry> _toolRegistryMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<ILogger<TaskExecutorClass>> _loggerMock;
    private readonly TaskExecutorClass _sut;

    public TaskExecutorScopeEnforcementTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _toolCallingLoopMock = new Mock<IToolCallingLoop>();
        _toolRegistryMock = new Mock<IToolRegistry>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _loggerMock = new Mock<ILogger<TaskExecutorClass>>();

        _workspaceMock.Setup(w => w.AosRootPath).Returns(Path.GetTempPath());

        _sut = new TaskExecutorClass(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _toolRegistryMock.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        Func<Task> act = async () => await _sut.ExecuteAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlanFileNotFound_ReturnsFailureResult()
    {
        using var tempDir = new TempDirectory();
        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/" });

        var result = await _sut.ExecuteAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Plan file not found");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlanContractInvalid_FailsFastWithDiagnosticAndNoRunCreated()
    {
        using var tempDir = new TempDirectory();
        var planPath = Path.Combine(tempDir.Path, "plan.json");
        await File.WriteAllTextAsync(planPath, "{\"fileScopes\":[\"src/file.cs\"]}");

        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/" });

        var result = await _sut.ExecuteAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Artifact contract validation failed");
        result.EvidenceArtifacts.Should().ContainSingle();
        File.Exists(result.EvidenceArtifacts[0]).Should().BeTrue();

        _runLifecycleManagerMock.Verify(x => x.StartRunAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlanFileScopeIsOutsideAllowedScope_ReturnsFailureResult()
    {
        using var tempDir = new TempDirectory();
        var plan = new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>(),
            FileScopes = new List<FileScopeEntry> { new() { Path = "outside/scope/file.cs" } }
        };
        WritePlanFile(tempDir.Path, plan);

        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/", "tests/" });

        var result = await _sut.ExecuteAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("outside allowed scope");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyAllowedFileScope_ReturnsFailureResult()
    {
        using var tempDir = new TempDirectory();
        var plan = new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>(),
            FileScopes = new List<FileScopeEntry> { new() { Path = "src/file.cs" } }
        };
        WritePlanFile(tempDir.Path, plan);

        var request = CreateRequest(tempDir.Path, allowedScopes: Array.Empty<string>());

        var result = await _sut.ExecuteAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No allowed file scope specified");
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepTargetPathIsOutsideScope_ReturnsFailureResult()
    {
        using var tempDir = new TempDirectory();
        var plan = new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>
            {
                new() { StepId = "step-1", TargetPath = "outside/scope/malicious.cs" }
            },
            FileScopes = new List<FileScopeEntry> { new() { Path = "src/" } }
        };
        WritePlanFile(tempDir.Path, plan);

        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/" });
        SetupSuccessfulToolCallingRun();
        SetupRunLifecycleManager();

        var result = await _sut.ExecuteAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("outside allowed scope");
    }

    [Theory]
    [InlineData("src/file.cs", "src/", true)]
    [InlineData("src/nested/file.cs", "src/", true)]
    [InlineData("src/file.cs", "src", true)]
    [InlineData("tests/file.cs", "src/", false)]
    [InlineData("../outside.cs", "src/", false)]
    [InlineData("src/file.cs", "src/,tests/", true)]
    [InlineData("tests/file.cs", "src/,tests/", true)]
    [InlineData("docs/file.md", "src/,tests/", false)]
    public void IsPathInAllowedScope_Scenarios(string path, string scopes, bool expectedAllowed)
    {
        var scopeList = scopes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        
        var method = typeof(TaskExecutorClass).GetMethod("IsPathInAllowedScope", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var result = (bool)method!.Invoke(null, new object[] { path, scopeList })!;

        result.Should().Be(expectedAllowed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllScopesValid_ProceedsWithExecution()
    {
        using var tempDir = new TempDirectory();
        var plan = new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>
            {
                new() { StepId = "step-1", TargetPath = "src/valid/file.cs" }
            },
            FileScopes = new List<FileScopeEntry> { new() { Path = "src/" } }
        };
        WritePlanFile(tempDir.Path, plan);

        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/" });
        SetupSuccessfulToolCallingRun(modifiedFiles: new[] { "src/valid/file.cs" });
        SetupRunLifecycleManager();

        var result = await _sut.ExecuteAsync(request);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopeValidationPasses_CreatesRunRecord()
    {
        using var tempDir = new TempDirectory();
        var plan = new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>(),
            FileScopes = new List<FileScopeEntry> { new() { Path = "src/" } }
        };
        WritePlanFile(tempDir.Path, plan);

        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/" });
        SetupSuccessfulToolCallingRun();
        SetupRunLifecycleManager(runId: "RUN-123");

        await _sut.ExecuteAsync(request);

        _runLifecycleManagerMock.Verify(x => x.StartRunAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopeValidationFails_DoesNotCreateRunRecord()
    {
        using var tempDir = new TempDirectory();
        var plan = new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>(),
            FileScopes = new List<FileScopeEntry> { new() { Path = "outside/scope/" } }
        };
        WritePlanFile(tempDir.Path, plan);

        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/" });

        await _sut.ExecuteAsync(request);

        _runLifecycleManagerMock.Verify(x => x.StartRunAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesBackslashPathSeparators_Correctly()
    {
        using var tempDir = new TempDirectory();
        var plan = new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>
            {
                new() { StepId = "step-1", TargetPath = "src\\file.cs" }
            },
            FileScopes = new List<FileScopeEntry> { new() { Path = "src/" } }
        };
        WritePlanFile(tempDir.Path, plan);

        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/" });
        SetupSuccessfulToolCallingRun();
        SetupRunLifecycleManager();

        var result = await _sut.ExecuteAsync(request);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_HandlesLeadingSlashes_Correctly()
    {
        using var tempDir = new TempDirectory();
        var plan = new TaskPlanModel
        {
            Title = "Test Plan",
            Steps = new List<PlanStep>
            {
                new() { StepId = "step-1", TargetPath = "/src/file.cs" }
            },
            FileScopes = new List<FileScopeEntry> { new() { Path = "src/" } }
        };
        WritePlanFile(tempDir.Path, plan);

        var request = CreateRequest(tempDir.Path, allowedScopes: new[] { "src/" });
        SetupSuccessfulToolCallingRun();
        SetupRunLifecycleManager();

        var result = await _sut.ExecuteAsync(request);

        result.Success.Should().BeTrue();
    }

    private TaskExecutionRequest CreateRequest(string taskDir, string[] allowedScopes)
    {
        return new TaskExecutionRequest
        {
            TaskId = "TSK-001",
            TaskDirectory = taskDir,
            AllowedFileScope = allowedScopes.ToList().AsReadOnly()
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
                StepId = string.IsNullOrWhiteSpace(step.StepId) ? "step-1" : step.StepId,
                StepType = string.IsNullOrWhiteSpace(step.StepType) ? "write_file" : step.StepType,
                TargetPath = step.TargetPath ?? string.Empty,
                Description = string.IsNullOrWhiteSpace(step.Description) ? "Execute step" : step.Description
            }).ToList(),
            VerificationSteps = plan.VerificationSteps
        };

        var json = JsonSerializer.Serialize(normalizedPlan, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(planPath, json);
    }

    private void SetupSuccessfulToolCallingRun(string[]? modifiedFiles = null)
    {
        var result = new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Task execution completed successfully"),
            ConversationHistory = new List<ToolCallingMessage>(),
            IterationCount = 1,
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            Usage = new ToolCallingUsageStats
            {
                TotalPromptTokens = 100,
                TotalCompletionTokens = 50,
                IterationCount = 1
            },
            Metadata = new Dictionary<string, string>
            {
                ["DurationMs"] = "1000"
            }
        };

        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupRunLifecycleManager(string runId = "RUN-001")
    {
        var context = new nirmata.Agents.Models.Runtime.RunContext { RunId = runId, StartedAt = DateTimeOffset.UtcNow };
        _runLifecycleManagerMock
            .Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}

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

public class TaskExecutorScopeEnforcementTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<ISubagentOrchestrator> _subagentOrchestratorMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<ILogger<TaskExecutor>> _loggerMock;
    private readonly TaskExecutor _sut;

    public TaskExecutorScopeEnforcementTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _subagentOrchestratorMock = new Mock<ISubagentOrchestrator>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _loggerMock = new Mock<ILogger<TaskExecutor>>();

        _workspaceMock.Setup(w => w.AosRootPath).Returns(Path.GetTempPath());

        _sut = new TaskExecutor(
            _runLifecycleManagerMock.Object,
            _subagentOrchestratorMock.Object,
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
        SetupSuccessfulSubagentRun();

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
        
        var method = typeof(TaskExecutor).GetMethod("IsPathInAllowedScope", 
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
        SetupSuccessfulSubagentRun(modifiedFiles: new[] { "src/valid/file.cs" });
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
        SetupSuccessfulSubagentRun();
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
        SetupSuccessfulSubagentRun();
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
        SetupSuccessfulSubagentRun();
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
        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(planPath, json);
    }

    private void SetupSuccessfulSubagentRun(string[]? modifiedFiles = null)
    {
        var result = new SubagentRunResult
        {
            Success = true,
            RunId = "SUB-001",
            TaskId = "TSK-001",
            ModifiedFiles = (modifiedFiles ?? Array.Empty<string>()).ToList().AsReadOnly()
        };

        _subagentOrchestratorMock
            .Setup(x => x.RunSubagentAsync(It.IsAny<SubagentRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupRunLifecycleManager(string runId = "RUN-001")
    {
        var context = new RunContext { RunId = runId, StartedAt = DateTimeOffset.UtcNow };
        _runLifecycleManagerMock
            .Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
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

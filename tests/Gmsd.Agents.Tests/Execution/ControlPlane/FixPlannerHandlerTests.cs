using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.FixPlanner;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Contracts.State;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane;

public class FixPlannerHandlerTests : IDisposable
{
    private readonly FakeWorkspace _workspace;
    private readonly FakeStateStore _stateStore;
    private readonly FakeRunLifecycleManager _runLifecycleManager;
    private readonly FakeEventStore _eventStore;
    private readonly Agents.Execution.FixPlanner.FixPlanner _fixPlanner;
    private readonly FixPlannerHandler _sut;

    public FixPlannerHandlerTests()
    {
        _workspace = new FakeWorkspace();
        _stateStore = new FakeStateStore();
        _runLifecycleManager = new FakeRunLifecycleManager();
        _eventStore = new FakeEventStore();

        // Create FixPlanner with all dependencies
        _fixPlanner = new Agents.Execution.FixPlanner.FixPlanner(
            _workspace, _stateStore, _runLifecycleManager, _eventStore);

        // Create handler
        _sut = new FixPlannerHandler(
            _fixPlanner, _workspace, _stateStore, _runLifecycleManager);

        // Set up initial state
        var initialSnapshot = new StateSnapshot
        {
            SchemaVersion = 1,
            Cursor = new StateCursor
            {
                MilestoneId = "milestone-1",
                MilestoneStatus = "active",
                PhaseId = "phase-1",
                PhaseStatus = "active",
                TaskId = "TSK-001",
                TaskStatus = "failed",
                StepId = "step-1",
                StepStatus = "failed"
            }
        };
        _stateStore.SetSnapshot(initialSnapshot);

        // Create state.json file
        var stateDir = Path.Combine(_workspace.AosRootPath, "state");
        Directory.CreateDirectory(stateDir);
        var stateJson = System.Text.Json.JsonSerializer.Serialize(initialSnapshot, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        File.WriteAllText(Path.Combine(stateDir, "state.json"), stateJson);

        // Create spec directories
        Directory.CreateDirectory(Path.Combine(_workspace.AosRootPath, "spec", "issues"));
        Directory.CreateDirectory(Path.Combine(_workspace.AosRootPath, "spec", "tasks"));
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task HandleAsync_WithValidIssues_ReturnsSuccessAndRoutesToTaskExecutor()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RoutingHint.Should().Be("TaskExecutor");
        result.Output.Should().Contain("Fix planning completed");
        result.Output.Should().Contain("TSK-001");
    }

    [Fact]
    public async Task HandleAsync_WithNoTaskInCursor_ReturnsFailure()
    {
        // Arrange - clear the state
        _stateStore.Reset();

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("No current task found");
    }

    [Fact]
    public async Task HandleAsync_WithNoIssues_ReturnsFailure()
    {
        // Arrange - no issues created

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("No issues found");
    }

    [Fact]
    public async Task HandleAsync_WithIssueIdsInOptions_UsesSpecifiedIssues()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>
            {
                { "issue-ids", issueId }
            }
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("1 issue(s)");
    }

    [Fact]
    public async Task HandleAsync_WithIssueIdInArguments_UsesSpecifiedIssue()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string> { $"--issue-id={issueId}" },
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("1 issue(s)");
    }

    [Fact]
    public async Task HandleAsync_WithContextPackIdInOptions_UsesSpecifiedContext()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>
            {
                { "issue-ids", issueId },
                { "context-pack-id", "custom-context-001" }
            }
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_CreatesFixTasksWithCorrectStructure()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify task artifacts were created
        var tasksDir = Path.Combine(_workspace.AosRootPath, "spec", "tasks");
        var taskDirs = Directory.GetDirectories(tasksDir, "TSK-FIX-*");
        taskDirs.Should().HaveCount(1);

        var taskDir = taskDirs[0];
        File.Exists(Path.Combine(taskDir, "task.json")).Should().BeTrue();
        File.Exists(Path.Combine(taskDir, "plan.json")).Should().BeTrue();
        File.Exists(Path.Combine(taskDir, "links.json")).Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithMultipleIssuesForSameTask_ConsolidatesIntoFixTasks()
    {
        // Arrange
        var issue1 = "ISS-0001";
        var issue2 = "ISS-0002";
        CreateTestIssue(issue1, "TSK-001", "src/Program.cs", "expected1", "actual1");
        CreateTestIssue(issue2, "TSK-001", "src/Service.cs", "expected2", "actual2");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("2 issue(s)");
    }

    [Fact]
    public async Task HandleAsync_RecordsCommandInLifecycleManager()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var commands = _runLifecycleManager.GetRecordedCommands();
        commands.Should().ContainSingle(c =>
            c.Group == "run" &&
            c.Command == "fix-plan" &&
            c.Status == "completed");
    }

    [Fact]
    public async Task HandleAsync_DiscoversIssuesFromParentTask()
    {
        // Arrange - create issues that reference TSK-001 (the parent task in cursor)
        var issue1 = "ISS-0001";
        var issue2 = "ISS-0002";
        CreateTestIssue(issue1, "TSK-001", "src/Program.cs", "expected1", "actual1");
        CreateTestIssue(issue2, "TSK-001", "src/Service.cs", "expected2", "actual2");

        // Create an issue for a different task (should not be discovered)
        CreateTestIssue("ISS-OTHER", "TSK-OTHER", "src/Other.cs", "expected", "actual");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("2 issue(s)");
        result.Output.Should().NotContain("3 issue"); // Should not include ISS-OTHER
    }

    [Fact]
    public async Task HandleAsync_WithInvalidIssueFile_SkipsInvalidFile()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        // Create an invalid issue file
        var issuesDir = Path.Combine(_workspace.AosRootPath, "spec", "issues");
        File.WriteAllText(Path.Combine(issuesDir, "ISS-INVALID.json"), "not valid json");

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("1 issue(s)"); // Only the valid issue
    }

    private void CreateTestIssue(string issueId, string taskId, string scope, string expected, string actual, string severity = "high")
    {
        var issuesDir = Path.Combine(_workspace.AosRootPath, "spec", "issues");
        Directory.CreateDirectory(issuesDir);

        var issue = new
        {
            schemaVersion = "gmsd:aos:schema:issue:v1",
            id = issueId,
            scope,
            repro = $"Test repro for {issueId}",
            expected,
            actual,
            severity,
            parentUatId = "criterion-001",
            taskId,
            runId = "run-001",
            timestamp = DateTimeOffset.UtcNow,
            dedupHash = Guid.NewGuid().ToString()[..16]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(issue);
        File.WriteAllText(Path.Combine(issuesDir, $"{issueId}.json"), json);
    }
}

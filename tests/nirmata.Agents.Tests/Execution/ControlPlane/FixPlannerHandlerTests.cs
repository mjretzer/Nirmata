using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.FixPlanner;
using nirmata.Agents.Tests.Fakes;
using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using nirmata.Common.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane;

public class FixPlannerHandlerTests : IDisposable
{
    private readonly FakeWorkspace _workspace;
    private readonly FakeStateStore _stateStore;
    private readonly FakeRunLifecycleManager _runLifecycleManager;
    private readonly FakeEventStore _eventStore;
    private readonly Mock<IClock> _clockMock;
    private readonly Agents.Execution.FixPlanner.FixPlanner _fixPlanner;
    private readonly FixPlannerHandler _sut;

    public FixPlannerHandlerTests()
    {
        _workspace = new FakeWorkspace();
        _stateStore = new FakeStateStore();
        _runLifecycleManager = new FakeRunLifecycleManager();
        _eventStore = new FakeEventStore();
        
        _clockMock = new Mock<IClock>();
        _clockMock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

        // Create FixPlanner with all dependencies
        _fixPlanner = new Agents.Execution.FixPlanner.FixPlanner(
            _workspace, _stateStore, _runLifecycleManager, _eventStore, _clockMock.Object);

        // Create handler
        _sut = new FixPlannerHandler(
            _fixPlanner, _workspace, _stateStore, _runLifecycleManager, NullLogger<FixPlannerHandler>.Instance);

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

        // Seed the run used in tests
        _runLifecycleManager.SeedRun("run-001");

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

    [Fact]
    public async Task HandleAsync_WithStructuredLlmFixPlan_EmitsStructuredPlanTelemetry()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        var fakeLlmProvider = new FakeLlmProvider()
            .EnqueueTextResponse(
                """
                {
                  "fixes": [
                    {
                      "issueId": "ISS-0001",
                      "description": "Apply null-check and branch guard for startup flow.",
                      "proposedChanges": [
                        {
                          "file": "src/Program.cs",
                          "changeDescription": "Add null-check around startup dependency before executing pipeline."
                        }
                      ],
                      "tests": [
                        "Run startup regression tests"
                      ]
                    }
                  ]
                }
                """);

        var fixPlanner = new Agents.Execution.FixPlanner.FixPlanner(
            _workspace,
            _stateStore,
            _runLifecycleManager,
            _eventStore,
            _clockMock.Object,
            fakeLlmProvider);

        var sut = new FixPlannerHandler(
            fixPlanner,
            _workspace,
            _stateStore,
            _runLifecycleManager,
            NullLogger<FixPlannerHandler>.Instance);

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var emittedEvents = _eventStore.GetEventsByType("fix-planning.plan-emitted");
        emittedEvents.Should().ContainSingle();
        emittedEvents[0].GetProperty("fixCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WhenStructuredFixPlanSchemaMismatch_ReturnsFailure()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "TSK-001", "src/Program.cs", "expected", "actual");

        var fakeLlmProvider = new FakeLlmProvider()
            .EnqueueTextResponse(
                """
                {
                  "fixes": [
                    {
                      "issueId": "ISS-0001",
                      "description": "Apply startup guard logic for null dependency handling.",
                      "proposedChanges": [
                        {
                          "file": "src/Program.cs",
                          "changeDescription": "Add null-check around startup dependency before executing pipeline."
                        }
                      ],
                      "tests": []
                    }
                  ]
                }
                """);

        var fixPlanner = new Agents.Execution.FixPlanner.FixPlanner(
            _workspace,
            _stateStore,
            _runLifecycleManager,
            _eventStore,
            _clockMock.Object,
            fakeLlmProvider);

        var sut = new FixPlannerHandler(
            fixPlanner,
            _workspace,
            _stateStore,
            _runLifecycleManager,
            NullLogger<FixPlannerHandler>.Instance);

        var request = new CommandRequest
        {
            Group = "fix-planner",
            Command = "fix-plan",
            Arguments = new List<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await sut.HandleAsync(request, "run-001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Fix planning failed");
        result.ErrorOutput.Should().Contain("Structured fix plan validation failed");

        var schemaFailureEvents = _eventStore.GetEventsByType("fix-planning.schema-validation-failed");
        schemaFailureEvents.Should().ContainSingle();
        schemaFailureEvents[0].GetProperty("failureKind").GetString().Should().Be("SchemaValidation");
    }

    [Fact]
    public async Task HandleAsync_WithoutLlmProvider_UsesFallbackStructuredPlanAndSucceeds()
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

        var emittedEvents = _eventStore.GetEventsByType("fix-planning.plan-emitted");
        emittedEvents.Should().ContainSingle();

        var schemaFailureEvents = _eventStore.GetEventsByType("fix-planning.schema-validation-failed");
        schemaFailureEvents.Should().BeEmpty();
    }

    private void CreateTestIssue(string issueId, string taskId, string scope, string expected, string actual, string severity = "high")
    {
        var issuesDir = Path.Combine(_workspace.AosRootPath, "spec", "issues");
        Directory.CreateDirectory(issuesDir);

        var issue = new
        {
            schemaVersion = "nirmata:aos:schema:issue:v1",
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

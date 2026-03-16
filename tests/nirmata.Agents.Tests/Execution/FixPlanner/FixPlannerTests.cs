using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.FixPlanner;
using nirmata.Agents.Tests.Fakes;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Engine.Validation;
using nirmata.Common.Helpers;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.FixPlanner;

public class FixPlannerTests : IDisposable
{
    private readonly FakeWorkspace _workspace;
    private readonly FakeStateStore _stateStore;
    private readonly FakeRunLifecycleManager _runLifecycleManager;
    private readonly FakeEventStore _eventStore;
    private readonly Mock<IClock> _clockMock;
    private readonly Agents.Execution.FixPlanner.FixPlanner _sut;

    public FixPlannerTests()
    {
        _workspace = new FakeWorkspace();
        _stateStore = new FakeStateStore();
        _runLifecycleManager = new FakeRunLifecycleManager();
        _eventStore = new FakeEventStore();
        _clockMock = new Mock<IClock>();
        
        // Setup fixed time for deterministic tests
        var fixedTime = new DateTimeOffset(2026, 2, 13, 3, 14, 15, TimeSpan.Zero);
        _clockMock.Setup(c => c.UtcNow).Returns(fixedTime);

        _sut = new Agents.Execution.FixPlanner.FixPlanner(
            _workspace, _stateStore, _runLifecycleManager, _eventStore, _clockMock.Object);

        // Set up state store with a valid snapshot
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

        // Create state.json file for cursor update tests
        var stateDir = Path.Combine(_workspace.AosRootPath, "state");
        Directory.CreateDirectory(stateDir);
        var stateJson = JsonSerializer.Serialize(initialSnapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
    public async Task PlanFixesAsync_WithValidIssues_ReturnsSuccess()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected behavior", "actual failure");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FixTaskIds.Should().HaveCount(1);
        result.IssueAnalysis.Should().HaveCount(1);
        result.IssueAnalysis[0].IssueId.Should().Be(issueId);
    }

    [Fact]
    public async Task PlanFixesAsync_WithMultipleIssues_ReturnsMultipleFixTasks()
    {
        // Arrange
        var issue1 = "ISS-0001";
        var issue2 = "ISS-0002";
        CreateTestIssue(issue1, "src/Program.cs", "expected1", "actual1");
        CreateTestIssue(issue2, "src/Service.cs", "expected2", "actual2");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issue1, issue2 },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IssueAnalysis.Should().HaveCount(2);
    }

    [Fact]
    public async Task PlanFixesAsync_WithOverlappingScopes_ConsolidatesIntoSingleTask()
    {
        // Arrange - create two issues with the same file scope
        var issue1 = "ISS-0001";
        var issue2 = "ISS-0002";
        CreateTestIssue(issue1, "src/Program.cs", "expected1", "actual1");
        CreateTestIssue(issue2, "src/Program.cs", "expected2", "actual2");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issue1, issue2 },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FixTaskIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task PlanFixesAsync_WithManyIssues_LimitsToMaxThreeTasks()
    {
        // Arrange - create 5 issues with different scopes
        var issues = new List<string>();
        for (int i = 1; i <= 5; i++)
        {
            var issueId = $"ISS-{i:D4}";
            issues.Add(issueId);
            CreateTestIssue(issueId, $"src/File{i}.cs", $"expected{i}", $"actual{i}");
        }

        var request = new FixPlannerRequest
        {
            IssueIds = issues,
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FixTaskIds.Count.Should().BeLessOrEqualTo(3);
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratesDeterministicTaskIds()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act - run twice with same inputs
        var result1 = await _sut.PlanFixesAsync(request);
        var result2 = await _sut.PlanFixesAsync(request);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.FixTaskIds[0].Should().Be(result2.FixTaskIds[0]);
        result1.FixTaskIds[0].Should().StartWith("TSK-FIX-TSK-001-001-");
    }

    [Fact]
    public async Task PlanFixesAsync_WithNonExistentIssues_ReturnsFailure()
    {
        // Arrange
        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { "ISS-NONEXISTENT" },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No issues found");
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratesTaskJsonWithCorrectMetadata()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-PARENT",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        var taskId = result.FixTaskIds[0];
        var taskDir = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId);
        var taskJsonPath = Path.Combine(taskDir, "task.json");

        File.Exists(taskJsonPath).Should().BeTrue();

        var taskJson = await File.ReadAllTextAsync(taskJsonPath);
        var task = JsonSerializer.Deserialize<JsonElement>(taskJson);

        task.GetProperty("id").GetString().Should().Be(taskId);
        task.GetProperty("type").GetString().Should().Be("fix");
        task.GetProperty("status").GetString().Should().Be("planned");
        task.GetProperty("parentTaskId").GetString().Should().Be("TSK-PARENT");
        task.GetProperty("issueIds")[0].GetString().Should().Be(issueId);
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratedTaskJson_PassesSchemaValidation()
    {
        // Arrange - ensure schemas are loaded
        var schemaContext = AosJsonSchemaInstanceValidator.TryCreateLocalContext(
            _workspace.RepositoryRootPath, out var errorMessage);
        schemaContext.Should().NotBeNull($"Failed to create schema context: {errorMessage}");

        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        var taskId = result.FixTaskIds[0];
        var taskJsonPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId, "task.json");

        var validationIssues = AosJsonSchemaInstanceValidator.ValidateJsonFileAgainstSchema(
            schemaContext!, taskJsonPath, "nirmata:aos:schema:task:v1");

        validationIssues.Should().BeEmpty("task.json should validate against task schema");
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratesPlanJsonWithFileScopes()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        var taskId = result.FixTaskIds[0];
        var taskDir = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId);
        var planJsonPath = Path.Combine(taskDir, "plan.json");

        File.Exists(planJsonPath).Should().BeTrue();

        var planJson = await File.ReadAllTextAsync(planJsonPath);
        var plan = JsonSerializer.Deserialize<JsonElement>(planJson);

        plan.GetProperty("taskId").GetString().Should().Be(taskId);
        plan.GetProperty("fileScopes").GetArrayLength().Should().Be(1);
        plan.GetProperty("fileScopes")[0].GetProperty("path").GetString().Should().Be("src/Program.cs");
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratedPlanJson_HasValidSchemaStructure()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert - validate plan.json structure
        var taskId = result.FixTaskIds[0];
        var planJsonPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId, "plan.json");

        var planJson = await File.ReadAllTextAsync(planJsonPath);
        var plan = JsonSerializer.Deserialize<JsonElement>(planJson);

        // Verify required fields exist and have correct types
        plan.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        plan.GetProperty("taskId").GetString().Should().NotBeNullOrEmpty();
        plan.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        plan.GetProperty("description").GetString().Should().NotBeNullOrEmpty();
        plan.GetProperty("fileScopes").ValueKind.Should().Be(JsonValueKind.Array);
        plan.GetProperty("steps").ValueKind.Should().Be(JsonValueKind.Array);
        plan.GetProperty("acceptanceCriteria").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratesLinksJsonWithCorrectReferences()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-PARENT",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        var taskId = result.FixTaskIds[0];
        var taskDir = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId);
        var linksJsonPath = Path.Combine(taskDir, "links.json");

        File.Exists(linksJsonPath).Should().BeTrue();

        var linksJson = await File.ReadAllTextAsync(linksJsonPath);
        var links = JsonSerializer.Deserialize<JsonElement>(linksJson);

        links.GetProperty("parent").GetProperty("type").GetString().Should().Be("task");
        links.GetProperty("parent").GetProperty("id").GetString().Should().Be("TSK-PARENT");
        links.GetProperty("issues").GetArrayLength().Should().Be(1);
        links.GetProperty("issues")[0].GetProperty("id").GetString().Should().Be(issueId);
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratedLinksJson_HasValidSchemaStructure()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert - validate links.json structure
        var taskId = result.FixTaskIds[0];
        var linksJsonPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId, "links.json");

        var linksJson = await File.ReadAllTextAsync(linksJsonPath);
        var links = JsonSerializer.Deserialize<JsonElement>(linksJson);

        // Verify required fields exist and have correct types
        links.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        links.GetProperty("parent").ValueKind.Should().Be(JsonValueKind.Object);
        links.GetProperty("parent").GetProperty("type").GetString().Should().NotBeNullOrEmpty();
        links.GetProperty("parent").GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        links.GetProperty("parent").GetProperty("relationship").GetString().Should().NotBeNullOrEmpty();
        links.GetProperty("issues").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratesAcceptanceCriteriaInPlan()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "File should compile successfully", "Compilation error");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        var taskId = result.FixTaskIds[0];
        var planJsonPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId, "plan.json");
        var planJson = await File.ReadAllTextAsync(planJsonPath);
        var plan = JsonSerializer.Deserialize<JsonElement>(planJson);

        plan.GetProperty("acceptanceCriteria").GetArrayLength().Should().BeGreaterThan(0);
        var criteria = plan.GetProperty("acceptanceCriteria")[0];
        criteria.GetProperty("description").GetString().Should().Contain("compile successfully");
        criteria.GetProperty("isRequired").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task PlanFixesAsync_CreatesCorrectNumberOfSteps()
    {
        // Arrange - two issues in same scope should consolidate to one fix task with 2 steps
        var issue1 = "ISS-0001";
        var issue2 = "ISS-0002";
        CreateTestIssue(issue1, "src/Program.cs", "expected1", "actual1");
        CreateTestIssue(issue2, "src/Program.cs", "expected2", "actual2");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issue1, issue2 },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert - should create 1 task with 2 steps
        result.FixTaskIds.Should().HaveCount(1);

        var taskId = result.FixTaskIds[0];
        var planJsonPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId, "plan.json");
        var planJson = await File.ReadAllTextAsync(planJsonPath);
        var plan = JsonSerializer.Deserialize<JsonElement>(planJson);

        plan.GetProperty("steps").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task PlanFixesAsync_IssueAnalysisContainsAffectedFiles()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Services/MyService.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IssueAnalysis.Should().HaveCount(1);
        result.IssueAnalysis[0].AffectedFiles.Should().Contain("src/Services/MyService.cs");
    }

    [Fact]
    public async Task PlanFixesAsync_IssueAnalysisContainsRootCause()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected behavior", "actual failure");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IssueAnalysis.Should().HaveCount(1);
        result.IssueAnalysis[0].RootCause.Should().Contain(issueId);
        result.IssueAnalysis[0].RootCause.Should().Contain("expected behavior");
        result.IssueAnalysis[0].RootCause.Should().Contain("actual failure");
    }

    [Fact]
    public async Task PlanFixesAsync_IssueAnalysisContainsRecommendedFixes()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "missing feature");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IssueAnalysis[0].RecommendedFixes.Should().HaveCountGreaterThan(0);
        result.IssueAnalysis[0].RecommendedFixes[0].TargetFile.Should().Be("src/Program.cs");
    }

    [Fact]
    public async Task PlanFixesAsync_DifferentParentTasksGenerateDifferentTaskIds()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request1 = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        var request2 = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-002",
            ContextPackId = "ctx-001"
        };

        // Act
        var result1 = await _sut.PlanFixesAsync(request1);
        var result2 = await _sut.PlanFixesAsync(request2);

        // Assert
        result1.FixTaskIds[0].Should().NotBe(result2.FixTaskIds[0]);
    }

    [Fact]
    public async Task PlanFixesAsync_GeneratedArtifacts_AreDeterministicallySerialized()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act - Run twice with same inputs
        var result1 = await _sut.PlanFixesAsync(request);
        var taskId1 = result1.FixTaskIds[0];
        var taskJsonPath1 = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId1, "task.json");
        var planJsonPath1 = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId1, "plan.json");
        var linksJsonPath1 = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId1, "links.json");

        var taskBytes1 = await File.ReadAllBytesAsync(taskJsonPath1);
        var planBytes1 = await File.ReadAllBytesAsync(planJsonPath1);
        var linksBytes1 = await File.ReadAllBytesAsync(linksJsonPath1);

        // Delete the task directory and regenerate
        Directory.Delete(Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId1), recursive: true);

        var result2 = await _sut.PlanFixesAsync(request);
        var taskId2 = result2.FixTaskIds[0];
        var taskJsonPath2 = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId2, "task.json");
        var planJsonPath2 = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId2, "plan.json");
        var linksJsonPath2 = Path.Combine(_workspace.AosRootPath, "spec", "tasks", taskId2, "links.json");

        var taskBytes2 = await File.ReadAllBytesAsync(taskJsonPath2);
        var planBytes2 = await File.ReadAllBytesAsync(planJsonPath2);
        var linksBytes2 = await File.ReadAllBytesAsync(linksJsonPath2);

        // Assert - Task IDs should be identical (deterministic)
        taskId1.Should().Be(taskId2);

        // Byte-for-byte comparison (deterministic serialization)
        taskBytes1.Should().Equal(taskBytes2, "task.json should be byte-for-byte identical");
        planBytes1.Should().Equal(planBytes2, "plan.json should be byte-for-byte identical");
        linksBytes1.Should().Equal(linksBytes2, "links.json should be byte-for-byte identical");

        // Verify UTF-8 without BOM and LF line endings
        var taskText = System.Text.Encoding.UTF8.GetString(taskBytes1);
        taskText.Should().NotContain("\r\n", "should use LF line endings");
        taskText.Should().EndWith("\n", "should end with trailing newline");
    }

    [Fact]
    public async Task PlanFixesAsync_UpdatesStateCursorToFixPlannerComplete()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
        File.Exists(statePath).Should().BeTrue();

        var stateJson = await File.ReadAllTextAsync(statePath);
        var state = JsonSerializer.Deserialize<StateSnapshot>(stateJson);

        state.Should().NotBeNull();
        state!.Cursor.Should().NotBeNull();
        state.Cursor.PhaseId.Should().Be("fix-planner");
        state.Cursor.PhaseStatus.Should().Be("completed");
        state.Cursor.TaskId.Should().Be("TSK-001");
        state.Cursor.TaskStatus.Should().Be("fix-planned");
        state.Cursor.StepStatus.Should().Be("ready-to-execute");
    }

    [Fact]
    public async Task PlanFixesAsync_PreservesRoadmapContextInState()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
        var stateJson = await File.ReadAllTextAsync(statePath);
        var state = JsonSerializer.Deserialize<StateSnapshot>(stateJson);

        // Verify context preservation (roadmap/phase position)
        state!.Cursor.MilestoneId.Should().Be("milestone-1");
        state.Cursor.MilestoneStatus.Should().Be("active");
    }

    [Fact]
    public async Task PlanFixesAsync_AppendsFixPlanningStartedEvent()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var startedEvents = _eventStore.GetEventsByType("fix-planning.started");
        startedEvents.Should().HaveCount(1);

        var startedEvent = startedEvents[0];
        startedEvent.GetProperty("parentTaskId").GetString().Should().Be("TSK-001");
        startedEvent.GetProperty("issueIds").EnumerateArray().Should().HaveCount(1);
        startedEvent.GetProperty("correlationId").GetString().Should().Be("ctx-001");
    }

    [Fact]
    public async Task PlanFixesAsync_AppendsFixPlanningCompletedEvent()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var completedEvents = _eventStore.GetEventsByType("fix-planning.completed");
        completedEvents.Should().HaveCount(1);

        var completedEvent = completedEvents[0];
        completedEvent.GetProperty("parentTaskId").GetString().Should().Be("TSK-001");
        completedEvent.GetProperty("issueCount").GetInt32().Should().Be(1);
        completedEvent.GetProperty("fixCount").GetInt32().Should().Be(1);
        completedEvent.GetProperty("taskCount").GetInt32().Should().Be(1);
        completedEvent.GetProperty("fixTaskIds").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task PlanFixesAsync_MultipleIssues_CreateCorrectNumberOfEvents()
    {
        // Arrange
        var issue1 = "ISS-0001";
        var issue2 = "ISS-0002";
        CreateTestIssue(issue1, "src/Program.cs", "expected1", "actual1");
        CreateTestIssue(issue2, "src/Service.cs", "expected2", "actual2");

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issue1, issue2 },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var allEvents = _eventStore.GetRecordedEvents();
        allEvents.Should().HaveCount(3); // started + plan-emitted + completed

        var startedEvents = _eventStore.GetEventsByType("fix-planning.started");
        startedEvents.Should().HaveCount(1);

        var emittedEvents = _eventStore.GetEventsByType("fix-planning.plan-emitted");
        emittedEvents.Should().HaveCount(1);

        var completedEvents = _eventStore.GetEventsByType("fix-planning.completed");
        completedEvents.Should().HaveCount(1);

        var completedEvent = completedEvents[0];
        completedEvent.GetProperty("issueCount").GetInt32().Should().Be(2);
        completedEvent.GetProperty("fixCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task PlanFixesAsync_WithMalformedStructuredLlmResponse_ReturnsFailureAndTelemetryEvent()
    {
        // Arrange
        var issueId = "ISS-0001";
        CreateTestIssue(issueId, "src/Program.cs", "expected", "actual");

        var fakeLlmProvider = new FakeLlmProvider()
            .EnqueueTextResponse("not-json");

        var sut = new Agents.Execution.FixPlanner.FixPlanner(
            _workspace,
            _stateStore,
            _runLifecycleManager,
            _eventStore,
            _clockMock.Object,
            fakeLlmProvider);

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { issueId },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not valid JSON");

        var schemaFailureEvents = _eventStore.GetEventsByType("fix-planning.schema-validation-failed");
        schemaFailureEvents.Should().ContainSingle();
        schemaFailureEvents[0].GetProperty("failureKind").GetString().Should().Be("InvalidJson");
    }

    [Fact]
    public async Task PlanFixesAsync_WithIncompleteIssueMappings_ReturnsFailureAndTelemetryEvent()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "src/Program.cs", "expected1", "actual1");
        CreateTestIssue("ISS-0002", "src/Service.cs", "expected2", "actual2");

        var fakeLlmProvider = new FakeLlmProvider()
            .EnqueueTextResponse(
                """
                {
                  "fixes": [
                    {
                      "issueId": "ISS-0001",
                      "description": "Apply guard logic for startup behavior with deterministic checks.",
                      "proposedChanges": [
                        {
                          "file": "src/Program.cs",
                          "changeDescription": "Add deterministic startup guard."
                        }
                      ],
                      "tests": [
                        "Run startup tests"
                      ]
                    }
                  ]
                }
                """);

        var sut = new Agents.Execution.FixPlanner.FixPlanner(
            _workspace,
            _stateStore,
            _runLifecycleManager,
            _eventStore,
            _clockMock.Object,
            fakeLlmProvider);

        var request = new FixPlannerRequest
        {
            IssueIds = new List<string> { "ISS-0001", "ISS-0002" },
            WorkspaceRoot = _workspace.RepositoryRootPath,
            ParentTaskId = "TSK-001",
            ContextPackId = "ctx-001"
        };

        // Act
        var result = await sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("missing issue mappings");

        var schemaFailureEvents = _eventStore.GetEventsByType("fix-planning.schema-validation-failed");
        schemaFailureEvents.Should().ContainSingle();
        schemaFailureEvents[0].GetProperty("failureKind").GetString().Should().Be("IncompleteMapping");
    }

    private void CreateTestIssue(string issueId, string scope, string expected, string actual, string severity = "high")
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
            taskId = "TSK-001",
            runId = "run-001",
            timestamp = DateTimeOffset.UtcNow,
            dedupHash = Guid.NewGuid().ToString()[..16]
        };

        var json = JsonSerializer.Serialize(issue);
        File.WriteAllText(Path.Combine(issuesDir, $"{issueId}.json"), json);
    }
}

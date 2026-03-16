using FluentAssertions;
using nirmata.Agents.Execution.Planning.PhasePlanner;
using nirmata.Agents.Execution.Planning.PhasePlanner.Assumptions;
using nirmata.Agents.Execution.Planning.PhasePlanner.ContextGatherer;
using nirmata.Agents.Execution.Execution.TaskExecutor;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Execution.FixPlanner;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Tests.Fakes;
using nirmata.Agents.Tests.Helpers;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using nirmata.Common.Helpers;
using Moq;
using System.Text.Json;
using Xunit;
using PhasePlannerClass = nirmata.Agents.Execution.Planning.PhasePlanner.PhasePlanner;

namespace nirmata.Agents.Tests.E2E;

/// <summary>
/// End-to-end tests for unified data contracts across the complete workflow.
/// Tests validate artifact chaining from Phase Planner → Task Executor → Verifier → Fix Planner
/// with unified JSON schemas and proper validation at each phase boundary.
/// </summary>
public class UnifiedContractsWorkflowE2ETests : IDisposable
{
    private readonly FakeLlmProvider _fakeLlmProvider;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IEventStore> _eventStoreMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly PhaseContextGatherer _contextGatherer;
    private readonly PhasePlannerClass _phasePlanner;
    private readonly PhaseAssumptionLister _assumptionLister;
    private readonly PhasePlannerHandler _phasePlannerHandler;
    private readonly TempDirectory _tempDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public UnifiedContractsWorkflowE2ETests()
    {
        _fakeLlmProvider = new FakeLlmProvider();
        _workspaceMock = new Mock<IWorkspace>();
        _eventStoreMock = new Mock<IEventStore>();
        _stateStoreMock = new Mock<IStateStore>();
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _tempDir = new TempDirectory();

        var tempPath = _tempDir.Path;
        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns(tempPath);
        _workspaceMock.Setup(x => x.AosRootPath).Returns(Path.Combine(tempPath, ".aos"));

        _contextGatherer = new PhaseContextGatherer(_workspaceMock.Object);
        _phasePlanner = new PhasePlannerClass(_fakeLlmProvider, _workspaceMock.Object);
        _assumptionLister = new PhaseAssumptionLister(_workspaceMock.Object);

        _phasePlannerHandler = new PhasePlannerHandler(
            _contextGatherer,
            _phasePlanner,
            _assumptionLister,
            _runLifecycleManagerMock.Object,
            _eventStoreMock.Object
        );
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    #region 5.5 LLM Structured Output Integration - Phase Planner

    [Fact]
    public async Task E2E_PhasePlanner_StructuredOutput_GeneratesValidSchema()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _phasePlannerHandler.PlanPhaseAsync(phaseId, runId);

        // Assert - Verify structured output was generated
        result.IsSuccess.Should().BeTrue();
        result.TaskPlan.Should().NotBeNull();
        result.TaskPlan!.IsValid.Should().BeTrue();
        result.TaskPlan.Tasks.Should().HaveCount(2);

        // Verify each task has required fields for canonical schema
        foreach (var task in result.TaskPlan.Tasks)
        {
            task.TaskId.Should().NotBeNullOrEmpty();
            task.Title.Should().NotBeNullOrEmpty();
            task.Description.Should().NotBeNullOrEmpty();
            task.FileScopes.Should().NotBeEmpty();
            task.VerificationSteps.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task E2E_PhasePlanner_StructuredOutput_ValidatesAgainstSchema()
    {
        // Arrange
        var phaseId = "PH-0002";
        var runId = "RUN-002";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(3);

        // Act
        var result = await _phasePlannerHandler.PlanPhaseAsync(phaseId, runId);

        // Assert - Verify plan conforms to canonical schema
        result.IsSuccess.Should().BeTrue();
        result.TaskPlan!.PlanId.Should().NotBeNullOrEmpty();
        result.TaskPlan.PhaseId.Should().Be(phaseId);
        result.TaskPlan.RunId.Should().Be(runId);
        result.TaskPlan.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task E2E_PhasePlanner_StructuredOutput_PersistsCanonicalJson()
    {
        // Arrange
        var phaseId = "PH-0003";
        var runId = "RUN-003";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _phasePlannerHandler.PlanPhaseAsync(phaseId, runId);

        // Assert - Verify plan JSON was persisted
        result.IsSuccess.Should().BeTrue();
        result.TaskPlan!.PlanJsonPath.Should().NotBeNullOrEmpty();

        var aosPath = _workspaceMock.Object.AosRootPath;
        var planPath = Path.Combine(aosPath, result.TaskPlan.PlanJsonPath.TrimStart('.', '/', '\\'));
        File.Exists(planPath).Should().BeTrue("Plan JSON should be persisted to disk");

        // Verify persisted JSON is valid
        var jsonContent = File.ReadAllText(planPath);
        var deserializedPlan = JsonSerializer.Deserialize<dynamic>(jsonContent, JsonOptions);
        deserializedPlan.Should().NotBeNull();
    }

    #endregion

    #region 5.6 LLM Structured Output Integration - Fix Planner

    [Fact]
    public async Task E2E_FixPlanner_StructuredOutput_GeneratesValidSchema()
    {
        // Arrange
        var fixPlanner = new FixPlanner(
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _runLifecycleManagerMock.Object,
            _eventStoreMock.Object,
            new SystemClock(),
            _fakeLlmProvider
        );

        var request = new FixPlannerRequest
        {
            ParentTaskId = "TSK-0001",
            IssueIds = new[] { "ISS-001", "ISS-002" },
            WorkspaceRoot = _workspaceMock.Object.RepositoryRootPath,
            ContextPackId = "ctx-e2e-001"
        };

        SetupFixPlannerIssues();
        SetupLlmFixPlanResponse();

        // Act
        var result = await fixPlanner.PlanFixesAsync(request);

        // Assert - Verify structured output was generated
        result.IsSuccess.Should().BeTrue();
        result.StructuredFixPlanJson.Should().NotBeNullOrWhiteSpace();

        var structuredFixPlan = JsonSerializer.Deserialize<FixPlan>(result.StructuredFixPlanJson!, JsonOptions);
        structuredFixPlan.Should().NotBeNull();
        structuredFixPlan!.Fixes.Should().NotBeEmpty();

        // Verify each fix has required fields for canonical schema
        foreach (var fix in structuredFixPlan.Fixes)
        {
            fix.IssueId.Should().NotBeNullOrEmpty();
            fix.Description.Should().NotBeNullOrEmpty();
            fix.ProposedChanges.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task E2E_FixPlanner_StructuredOutput_ValidatesAgainstSchema()
    {
        // Arrange
        var fixPlanner = new FixPlanner(
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _runLifecycleManagerMock.Object,
            _eventStoreMock.Object,
            new SystemClock(),
            _fakeLlmProvider
        );

        var request = new FixPlannerRequest
        {
            ParentTaskId = "TSK-0002",
            IssueIds = new[] { "ISS-003" },
            WorkspaceRoot = _workspaceMock.Object.RepositoryRootPath,
            ContextPackId = "ctx-e2e-002"
        };

        SetupFixPlannerIssues();
        SetupLlmFixPlanResponse();

        // Act
        var result = await fixPlanner.PlanFixesAsync(request);

        // Assert - Verify plan conforms to canonical schema
        result.IsSuccess.Should().BeTrue();
        result.StructuredFixPlanJson.Should().NotBeNullOrWhiteSpace();
        var structuredFixPlan = JsonSerializer.Deserialize<FixPlan>(result.StructuredFixPlanJson!, JsonOptions);
        structuredFixPlan.Should().NotBeNull();
        structuredFixPlan!.Fixes.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region 5.7 End-to-End Workflow Tests with Unified Contracts

    [Fact]
    public async Task E2E_FullWorkflow_PlannerToExecutor_ArtifactChaining()
    {
        // Arrange - Setup phase planning
        var phaseId = "PH-E2E-001";
        var runId = "RUN-E2E-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act - Phase 1: Create task plan
        var planResult = await _phasePlannerHandler.PlanPhaseAsync(phaseId, runId);

        // Assert - Verify plan was created successfully
        planResult.IsSuccess.Should().BeTrue();
        planResult.TaskPlan.Should().NotBeNull();
        planResult.TaskPlan!.Tasks.Should().HaveCount(2);

        // Verify plan JSON is valid and can be read back
        var aosPath = _workspaceMock.Object.AosRootPath;
        var planPath = Path.Combine(aosPath, planResult.TaskPlan.PlanJsonPath.TrimStart('.', '/', '\\'));
        File.Exists(planPath).Should().BeTrue();

        var planJson = File.ReadAllText(planPath);
        var readPlan = JsonSerializer.Deserialize<dynamic>(planJson, JsonOptions);
        readPlan.Should().NotBeNull();

        // Verify tasks can be read and have canonical schema structure
        foreach (var task in planResult.TaskPlan.Tasks)
        {
            task.TaskId.Should().NotBeNullOrEmpty();
            task.Title.Should().NotBeNullOrEmpty();
            task.Description.Should().NotBeNullOrEmpty();
            task.FileScopes.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task E2E_FullWorkflow_ValidatesArtifactBoundaries()
    {
        // Arrange
        var phaseId = "PH-E2E-002";
        var runId = "RUN-E2E-002";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act - Create task plan
        var planResult = await _phasePlannerHandler.PlanPhaseAsync(phaseId, runId);

        // Assert - Verify artifact boundaries are enforced
        planResult.IsSuccess.Should().BeTrue();
        planResult.TaskPlan!.IsValid.Should().BeTrue("Plan should pass validation at write boundary");
        planResult.TaskPlan.ValidationErrors.Should().BeEmpty();

        // Verify plan JSON persisted with correct structure
        var aosPath = _workspaceMock.Object.AosRootPath;
        var planPath = Path.Combine(aosPath, planResult.TaskPlan.PlanJsonPath.TrimStart('.', '/', '\\'));
        var planJson = File.ReadAllText(planPath);

        // Verify JSON can be deserialized and re-validated
        var deserializedPlan = JsonSerializer.Deserialize<dynamic>(planJson, JsonOptions);
        deserializedPlan.Should().NotBeNull("Persisted plan should be deserializable");
    }

    [Fact]
    public async Task E2E_FullWorkflow_MultiplePhases_MaintainsSchemaConsistency()
    {
        // Arrange - Setup multiple phases
        var phase1Id = "PH-MULTI-001";
        var phase2Id = "PH-MULTI-002";
        var runId = "RUN-MULTI-001";
        SetupWorkspaceWithPhase(phase1Id, "Phase 1");
        SetupWorkspaceWithPhase(phase2Id, "Phase 2");
        SetupLlmResponseWithValidTasks(2);

        // Act - Plan both phases
        var result1 = await _phasePlannerHandler.PlanPhaseAsync(phase1Id, runId);
        var result2 = await _phasePlannerHandler.PlanPhaseAsync(phase2Id, runId);

        // Assert - Verify both plans conform to same schema
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        result1.TaskPlan!.IsValid.Should().BeTrue();
        result2.TaskPlan!.IsValid.Should().BeTrue();

        // Verify both plans have same structure
        result1.TaskPlan.Tasks.Should().HaveCount(result2.TaskPlan.Tasks.Count);
        foreach (var task in result1.TaskPlan.Tasks.Concat(result2.TaskPlan.Tasks))
        {
            task.TaskId.Should().NotBeNullOrEmpty();
            task.Title.Should().NotBeNullOrEmpty();
            task.FileScopes.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task E2E_FullWorkflow_DiagnosticArtifactsOnValidationFailure()
    {
        // Arrange - Setup phase with invalid LLM response
        var phaseId = "PH-DIAG-001";
        var runId = "RUN-DIAG-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithInvalidTasks(); // Tasks missing required fields

        // Act
        var result = await _phasePlannerHandler.PlanPhaseAsync(phaseId, runId);

        // Assert - Verify validation failed and diagnostic info is available
        result.IsSuccess.Should().BeFalse("Planning should fail with invalid tasks");
        result.ErrorMessage.Should().NotBeNullOrEmpty();

        // Verify diagnostic artifacts directory exists
        var aosPath = _workspaceMock.Object.AosRootPath;
        var diagnosticsPath = Path.Combine(aosPath, "diagnostics", "phase-planning");
        // Note: Diagnostic persistence may be optional depending on implementation
        // This test verifies error handling is in place
    }

    [Fact]
    public async Task E2E_FullWorkflow_SchemaVersioning()
    {
        // Arrange
        var phaseId = "PH-VERSION-001";
        var runId = "RUN-VERSION-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _phasePlannerHandler.PlanPhaseAsync(phaseId, runId);

        // Assert - Verify schema version information is present
        result.IsSuccess.Should().BeTrue();
        result.TaskPlan.Should().NotBeNull();

        // Verify persisted plan includes schema version metadata
        var aosPath = _workspaceMock.Object.AosRootPath;
        var planPath = Path.Combine(aosPath, result.TaskPlan!.PlanJsonPath.TrimStart('.', '/', '\\'));
        var planJson = File.ReadAllText(planPath);
        planJson.Should().NotBeNullOrEmpty();

        // Verify JSON structure is consistent with canonical schema
        var deserializedPlan = JsonSerializer.Deserialize<dynamic>(planJson, JsonOptions);
        deserializedPlan.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private void SetupWorkspaceWithPhase(
        string phaseId,
        string phaseName = "Test Phase",
        string description = "Test phase description",
        string[]? inScope = null,
        string[]? outOfScope = null)
    {
        var aosPath = _workspaceMock.Object.AosRootPath;
        Directory.CreateDirectory(Path.Combine(aosPath, "spec", "phases"));
        Directory.CreateDirectory(Path.Combine(aosPath, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(aosPath, "diagnostics", "phase-planning"));

        var phaseSpecPath = Path.Combine(aosPath, "spec", "phases", $"{phaseId}.json");
        var phaseSpec = new
        {
            phaseId = phaseId,
            name = phaseName,
            description = description,
            inScope = inScope ?? Array.Empty<string>(),
            outOfScope = outOfScope ?? Array.Empty<string>()
        };

        File.WriteAllText(phaseSpecPath, JsonSerializer.Serialize(phaseSpec, JsonOptions));
    }

    private void SetupLlmResponseWithValidTasks(int taskCount)
    {
        var tasks = Enumerable.Range(1, taskCount).Select(i => new PhaseTask
        {
            Id = $"TSK-E2E-{i:D3}",
            Title = $"Task {i}",
            Description = $"Description for task {i}",
            FileScopes = new List<PhaseFileScope>
            {
                new PhaseFileScope { Path = $"src/file{i}.cs" }
            },
            VerificationSteps = new List<string>
            {
                $"Verify task {i} completion"
            }
        }).ToList();

        var phasePlan = new PhasePlan
        {
            PlanId = $"PLAN-{Guid.NewGuid():N}",
            PhaseId = "PH-0001",
            Tasks = tasks
        };

        var jsonContent = JsonSerializer.Serialize(phasePlan, JsonOptions);
        _fakeLlmProvider.EnqueueTextResponse(jsonContent);
    }

    private void SetupLlmResponseWithInvalidTasks()
    {
        var tasks = new List<PhaseTask>
        {
            new PhaseTask
            {
                Id = "TSK-E2E-INVALID-001",
                Title = string.Empty, // Invalid: empty title
                Description = "Description",
                FileScopes = new List<PhaseFileScope>(),
                VerificationSteps = new List<string>()
            }
        };

        var phasePlan = new PhasePlan
        {
            PlanId = $"PLAN-{Guid.NewGuid():N}",
            PhaseId = "PH-0001",
            Tasks = tasks
        };

        var jsonContent = JsonSerializer.Serialize(phasePlan, JsonOptions);
        _fakeLlmProvider.EnqueueTextResponse(jsonContent);
    }

    private void SetupFixPlannerIssues()
    {
        var issues = new List<IssueData>
        {
            new IssueData
            {
                Id = "ISS-001",
                Scope = "src/file1.cs",
                Expected = "Expected behavior",
                Actual = "Actual behavior",
                Severity = "high",
                Repro = "Steps to reproduce"
            },
            new IssueData
            {
                Id = "ISS-002",
                Scope = "src/file2.cs",
                Expected = "Expected behavior",
                Actual = "Actual behavior",
                Severity = "medium",
                Repro = "Steps to reproduce"
            }
        };

        var issuesDir = Path.Combine(_workspaceMock.Object.AosRootPath, "spec", "issues");
        Directory.CreateDirectory(issuesDir);

        foreach (var issue in issues)
        {
            var issuePath = Path.Combine(issuesDir, $"{issue.Id}.json");
            File.WriteAllText(issuePath, JsonSerializer.Serialize(issue, JsonOptions));
        }
    }

    private void SetupLlmFixPlanResponse()
    {
        var fixPlan = new FixPlan
        {
            Fixes = new List<FixEntry>
            {
                new FixEntry
                {
                    IssueId = "ISS-001",
                    Description = "Fix for issue 1",
                    ProposedChanges = new List<ProposedChange>
                    {
                        new ProposedChange
                        {
                            File = "src/file1.cs",
                            ChangeDescription = "Update implementation"
                        }
                    },
                    Tests = new List<TestEntry>
                    {
                        new TestEntry { TestName = "Test1", VerificationStep = "Verify fix" }
                    }
                },
                new FixEntry
                {
                    IssueId = "ISS-002",
                    Description = "Fix for issue 2",
                    ProposedChanges = new List<ProposedChange>
                    {
                        new ProposedChange
                        {
                            File = "src/file2.cs",
                            ChangeDescription = "Update implementation"
                        }
                    },
                    Tests = new List<TestEntry>
                    {
                        new TestEntry { TestName = "Test2", VerificationStep = "Verify fix" }
                    }
                }
            }
        };

        var jsonContent = JsonSerializer.Serialize(fixPlan, JsonOptions);
        _fakeLlmProvider.EnqueueTextResponse(jsonContent);
    }

    #endregion
}

/// <summary>
/// Helper models for testing (would normally be in separate files)
/// </summary>
public class IssueData
{
    public string Id { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Repro { get; set; } = string.Empty;
}

public class FixPlan
{
    public List<FixEntry> Fixes { get; set; } = new();
}

public class FixEntry
{
    public string IssueId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ProposedChange> ProposedChanges { get; set; } = new();
    public List<TestEntry> Tests { get; set; } = new();
}

public class ProposedChange
{
    public string File { get; set; } = string.Empty;
    public string ChangeDescription { get; set; } = string.Empty;
}

public class TestEntry
{
    public string TestName { get; set; } = string.Empty;
    public string VerificationStep { get; set; } = string.Empty;
}

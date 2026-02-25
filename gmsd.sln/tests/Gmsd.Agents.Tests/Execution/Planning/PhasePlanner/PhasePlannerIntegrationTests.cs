using FluentAssertions;
using Gmsd.Agents.Execution.Planning.PhasePlanner;
using Gmsd.Agents.Execution.Planning.PhasePlanner.Assumptions;
using Gmsd.Agents.Execution.Planning.PhasePlanner.ContextGatherer;
using PhasePlannerClass = Gmsd.Agents.Execution.Planning.PhasePlanner.PhasePlanner;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Agents.Tests.Helpers;
using Gmsd.Aos.Public;
using Moq;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Planning.PhasePlanner;

public class PhasePlannerIntegrationTests : IDisposable
{
    private readonly FakeLlmProvider _fakeLlmProvider;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IEventStore> _eventStoreMock;
    private readonly PhaseContextGatherer _contextGatherer;
    private readonly PhasePlannerClass _phasePlanner;
    private readonly PhaseAssumptionLister _assumptionLister;
    private readonly PhasePlannerHandler _handler;
    private readonly TempDirectory _tempDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public PhasePlannerIntegrationTests()
    {
        _fakeLlmProvider = new FakeLlmProvider();
        _workspaceMock = new Mock<IWorkspace>();
        _eventStoreMock = new Mock<IEventStore>();
        _tempDir = new TempDirectory();

        var tempPath = _tempDir.Path;
        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns(tempPath);
        _workspaceMock.Setup(x => x.AosRootPath).Returns(Path.Combine(tempPath, ".aos"));

        _contextGatherer = new PhaseContextGatherer(_workspaceMock.Object);
        _phasePlanner = new PhasePlannerClass(_fakeLlmProvider, _workspaceMock.Object);
        _assumptionLister = new PhaseAssumptionLister(_workspaceMock.Object);

        var runLifecycleManagerMock = new Mock<IRunLifecycleManager>();

        _handler = new PhasePlannerHandler(
            _contextGatherer,
            _phasePlanner,
            _assumptionLister,
            runLifecycleManagerMock.Object,
            _eventStoreMock.Object
        );
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_WithValidPhase_ReturnsSuccess()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_PopulatesPhaseBrief()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId, "Test Phase Name", "Test Description");
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);

        // Assert
        result.PhaseBrief.Should().NotBeNull();
        result.PhaseBrief!.PhaseId.Should().Be(phaseId);
        result.PhaseBrief.PhaseName.Should().Be("Test Phase Name");
        result.PhaseBrief.Description.Should().Be("Test Description");
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_CreatesTaskPlan()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(3);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);

        // Assert
        result.IsSuccess.Should().BeTrue($"because planning should succeed. Error: {result.ErrorMessage}");
        result.TaskPlan.Should().NotBeNull();
        result.TaskPlan!.Tasks.Should().NotBeNullOrEmpty();
        result.TaskPlan.Tasks.Should().HaveCount(3);
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_CreatesAssumptions()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId, inScope: new[] { "Feature A", "Feature B" });
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);

        // Assert
        result.Assumptions.Should().NotBeNull();
        result.Assumptions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_GeneratesAssumptionsDocument()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = $"RUN-{Guid.NewGuid():N}";
        SetupWorkspaceWithPhase(phaseId, inScope: new[] { "Feature A" });
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);

        // Assert
        result.AssumptionsDocumentPath.Should().NotBeNullOrEmpty();
        result.AssumptionsDocumentPath.Should().Contain("assumptions.md");
        File.Exists(result.AssumptionsDocumentPath).Should().BeTrue();

        // Cleanup
        CleanupRunEvidence(runId);
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_PersistsPlanningDecision()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);

        // Assert
        _eventStoreMock.Verify(x => x.AppendEvent(It.Is<JsonElement>(e =>
            e.GetProperty("eventType").GetString() == "phase.planned" &&
            e.GetProperty("runId").GetString() == runId &&
            e.GetProperty("data").GetProperty("phaseId").GetString() == phaseId
        )), Times.Once);
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_SetsStartedAt()
    {
        // Arrange
        var beforeTest = DateTimeOffset.UtcNow;
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);
        var afterTest = DateTimeOffset.UtcNow;

        // Assert
        result.StartedAt.Should().BeOnOrAfter(beforeTest);
        result.StartedAt.Should().BeOnOrBefore(afterTest);
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_SetsCompletedAt()
    {
        // Arrange
        var beforeTest = DateTimeOffset.UtcNow;
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);
        var afterTest = DateTimeOffset.UtcNow;

        // Assert
        result.CompletedAt.Should().NotBeNull();
        result.CompletedAt.Should().BeOnOrAfter(beforeTest);
        result.CompletedAt.Should().BeOnOrBefore(afterTest);
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_WhenTaskValidationFails_ReturnsFailure()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithTooManyTasks(5);

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PlanPhaseAsync_EndToEnd_WhenPhaseNotFound_ReturnsFailure()
    {
        // Arrange
        var phaseId = "PH-9999";
        var runId = "RUN-001";
        // Do NOT create phase file

        // Act
        var result = await _handler.PlanPhaseAsync(phaseId, runId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_CommandRequest_ExtractsPhaseIdFromOptions()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
        {
            Group = "phase",
            Command = "plan",
            Options = new Dictionary<string, string?> { { "phase-id", phaseId } },
            Arguments = Array.Empty<string>()
        };

        // Act
        var result = await _handler.HandleAsync(request, runId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_CommandRequest_ExtractsPhaseIdFromArguments()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "RUN-001";
        SetupWorkspaceWithPhase(phaseId);
        SetupLlmResponseWithValidTasks(2);

        var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
        {
            Group = "phase",
            Command = "plan",
            Options = new Dictionary<string, string?>(),
            Arguments = new List<string> { $"--phase-id={phaseId}" }
        };

        // Act
        var result = await _handler.HandleAsync(request, runId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_CommandRequest_WhenPhaseIdMissing_ReturnsFailure()
    {
        // Arrange
        var runId = "RUN-001";
        var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
        {
            Group = "phase",
            Command = "plan",
            Options = new Dictionary<string, string?>(),
            Arguments = new List<string>()
        };

        // Act
        var result = await _handler.HandleAsync(request, runId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Phase ID is required");
    }

    private void SetupWorkspaceWithPhase(
        string phaseId,
        string phaseName = "Test Phase",
        string description = "Test Description",
        string[]? inScope = null)
    {
        var phaseDoc = new
        {
            phaseId,
            name = phaseName,
            description,
            milestoneId = "MS-0001",
            inScope = inScope ?? Array.Empty<string>(),
            outOfScope = Array.Empty<string>(),
            scopeBoundaries = Array.Empty<string>(),
            inputArtifacts = new[] { "project.json" },
            outputArtifacts = new[] { "tasks/" }
        };

        var phasePath = Path.Combine(_workspaceMock.Object.AosRootPath, "spec", "phases", phaseId, "phase.json");
        Console.WriteLine($"DEBUG: SetupWorkspaceWithPhase - Writing phase to: {phasePath}");
        Directory.CreateDirectory(Path.GetDirectoryName(phasePath)!);
        File.WriteAllText(phasePath, JsonSerializer.Serialize(phaseDoc, JsonOptions));

        var roadmapDoc = new
        {
            roadmap = new
            {
                items = new[] { new { id = phaseId, title = phaseName } }
            }
        };
        var roadmapPath = Path.Combine(_workspaceMock.Object.AosRootPath, "spec", "roadmap.json");
        Directory.CreateDirectory(Path.GetDirectoryName(roadmapPath)!);
        File.WriteAllText(roadmapPath, JsonSerializer.Serialize(roadmapDoc, JsonOptions));

        var projectDoc = new
        {
            project = new { name = "Test Project" }
        };
        var projectPath = Path.Combine(_workspaceMock.Object.AosRootPath, "spec", "project.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, JsonSerializer.Serialize(projectDoc, JsonOptions));
    }

    private void SetupLlmResponseWithValidTasks(int taskCount)
    {
        var response = CreatePhasePlanResponse(taskCount);
        _fakeLlmProvider.EnqueueTextResponse(response);
    }

    private void SetupLlmResponseWithTooManyTasks(int taskCount)
    {
        var response = CreatePhasePlanResponse(taskCount);
        _fakeLlmProvider.EnqueueTextResponse(response);
    }

    private static string CreatePhasePlanResponse(int taskCount)
    {
        var tasks = new List<object>();
        for (int i = 1; i <= taskCount; i++)
        {
            tasks.Add(new
            {
                id = $"TSK-{i:000}",
                title = $"Task {i}",
                description = $"Description for task {i}",
                fileScopes = new[] { $"src/file{i}.cs" },
                verificationSteps = new[] { "Run unit tests" }
            });
        }

        var response = new
        {
            planId = $"PLAN-{Guid.NewGuid():N}",
            phaseId = "PH-0001",
            tasks
        };

        return JsonSerializer.Serialize(response);
    }

    private void CleanupRunEvidence(string runId)
    {
        try
        {
            var runPath = Path.Combine(_workspaceMock.Object.AosRootPath, "evidence", "runs", runId);
            if (Directory.Exists(runPath))
            {
                Directory.Delete(runPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

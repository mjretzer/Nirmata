using System.Text.Json;
using FluentAssertions;
using ExecutionOrchestrator = Gmsd.Agents.Execution.ControlPlane.Orchestrator;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.Planning;
using Gmsd.Agents.Models.Results;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Agents.Tests.Fixtures;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Services;
using Moq;
using Xunit;
using Gmsd.Agents.Execution.Validation;

namespace Gmsd.Agents.Tests.Integration.Orchestrator;

/// <summary>
/// End-to-end tests for the Orchestrator that verify the full workflow:
/// StartRun → Gate → Dispatch → CloseRun
/// Uses real file system for evidence folder verification.
/// </summary>
public class OrchestratorEndToEndTests : IDisposable
{
    private readonly AosTestWorkspaceBuilder _workspaceBuilder;
    private readonly HandlerTestHost _testHost;
    private readonly ExecutionOrchestrator _sut;

    public OrchestratorEndToEndTests()
    {
        // Create workspace builder with project for routing to Roadmapper
        _workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description");

        // Build the workspace to create the temp directory structure
        var workspace = _workspaceBuilder.Build();

        // Create test host with the workspace path
        _testHost = new HandlerTestHost(workspace.RepositoryRootPath);

        // Override the IWorkspace registration with our test workspace
        _testHost.OverrideWithInstance<IWorkspace>(workspace);

        // Get the orchestrator from DI
        _sut = _testHost.GetRequiredService<IOrchestrator>() as ExecutionOrchestrator
            ?? throw new InvalidOperationException("Could not resolve Orchestrator from DI");
    }

    public void Dispose()
    {
        // Dispose in reverse order of creation
        _testHost.Dispose();
        _workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WhenWorkspaceInitializationFails_ReturnsStructuredConversationalRecovery()
    {
        // Arrange
        var missing = new MissingPrerequisiteDetail
        {
            Type = PrerequisiteType.State,
            Description = "Failed to establish deterministic workspace state readiness: invalid NDJSON.",
            ExpectedPath = ".aos/state/",
            SuggestedCommand = "/init",
            FailureCode = "state-readiness-failure",
            FailingPrerequisite = ".aos/state/state.json",
            AttemptedRepairs =
            [
                "Ensure .aos/state/events.ndjson exists",
                "Ensure .aos/state/state.json exists with deterministic baseline",
                "Derive deterministic state snapshot from ordered events when snapshot is missing or stale"
            ],
            SuggestedFixes =
            [
                "Run /init to re-seed workspace state artifacts.",
                "Validate .aos/state/events.ndjson contains only valid JSON object lines.",
                "Re-run the workflow command after state repairs complete."
            ],
            RecoveryAction = "Repair workspace state artifacts and retry the command",
            ConversationalPrompt = "I couldn't repair workspace state readiness automatically. Please run /init and retry."
        };

        var prerequisiteValidator = new Mock<IPrerequisiteValidator>();
        prerequisiteValidator
            .Setup(v => v.EnsureWorkspaceInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrerequisiteValidationResult.NotSatisfied("WorkspaceInitialization", missing));

        _testHost.OverrideWithInstance(prerequisiteValidator.Object);

        var orchestrator = _testHost.GetRequiredService<IOrchestrator>() as ExecutionOrchestrator
            ?? throw new InvalidOperationException("Could not resolve Orchestrator from DI");

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap for my app",
            CorrelationId = "corr-e2e-init-fail"
        };

        // Act
        var result = await orchestrator.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalPhase.Should().Be("PrerequisiteCheck");
        result.RunId.Should().BeNull();
        result.Artifacts["failureCode"].Should().Be("state-readiness-failure");
        result.Artifacts["failingPrerequisite"].Should().Be(".aos/state/state.json");
        result.Artifacts["recoveryAction"].Should().Be("Repair workspace state artifacts and retry the command");
        result.Artifacts["requiresUserAction"].Should().Be(true);

        result.Artifacts["attemptedRepairs"].Should().BeAssignableTo<IReadOnlyList<string>>();
        result.Artifacts["suggestedFixes"].Should().BeAssignableTo<IReadOnlyList<string>>();
        ((IReadOnlyList<string>)result.Artifacts["attemptedRepairs"]).Should().Contain("Ensure .aos/state/events.ndjson exists");
        ((IReadOnlyList<string>)result.Artifacts["suggestedFixes"]).Should().Contain("Run /init to re-seed workspace state artifacts.");

        prerequisiteValidator.Verify(v => v.EnsureWorkspaceInitializedAsync(It.IsAny<CancellationToken>()), Times.Once);
        prerequisiteValidator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<GatingContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightValidationFails_ReturnsFailure()
    {
        // Arrange
        var mockValidator = new Mock<IPreflightValidator>();
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Gmsd.Agents.Execution.Validation.ValidationResult
            {
                Issues = new[] { new ValidationIssue { IssueType = "TestFailure", Message = "Preflight check failed", Severity = ValidationSeverity.Error } }
            });

        _testHost.OverrideWithInstance(mockValidator.Object);

        var orchestrator = _testHost.GetRequiredService<IOrchestrator>() as ExecutionOrchestrator
            ?? throw new InvalidOperationException("Could not resolve Orchestrator from DI");

        var intent = new WorkflowIntent
        {
            InputRaw = "test input",
            CorrelationId = "corr-e2e-preflight-fail"
        };

        // Act
        var result = await orchestrator.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalPhase.Should().Be("PreflightValidation");
        result.Artifacts.Should().ContainKey("TestFailure");

        // Verify summary.json shows failure
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        var summaryJsonPath = Path.Combine(evidenceFolder, "summary.json");
        var summaryJson = File.ReadAllText(summaryJsonPath);
        using var doc = JsonDocument.Parse(summaryJson);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("failed");
        root.GetProperty("outputs").GetProperty("TestFailure").GetString().Should().Be("Preflight check failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputValidationFails_ReturnsFailure()
    {
        // Arrange
        var mockValidator = new Mock<IOutputValidator>();
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<OrchestratorResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Gmsd.Agents.Execution.Validation.ValidationResult
            {
                Issues = new[] { new ValidationIssue { IssueType = "OutputTestFailure", Message = "Output check failed", Severity = ValidationSeverity.Error } }
            });

        _testHost.OverrideWithInstance(mockValidator.Object);

        var orchestrator = _testHost.GetRequiredService<IOrchestrator>() as ExecutionOrchestrator
            ?? throw new InvalidOperationException("Could not resolve Orchestrator from DI");

        var intent = new WorkflowIntent
        {
            InputRaw = "test input",
            CorrelationId = "corr-e2e-output-fail"
        };

        // Act
        var result = await orchestrator.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalPhase.Should().Be("OutputValidation");
        result.Artifacts.Should().ContainKey("OutputTestFailure");

        // Verify summary.json shows failure
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        var summaryJsonPath = Path.Combine(evidenceFolder, "summary.json");
        var summaryJson = File.ReadAllText(summaryJsonPath);
        using var doc = JsonDocument.Parse(summaryJson);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("failed");
        root.GetProperty("outputs").GetProperty("OutputTestFailure").GetString().Should().Be("Output check failed");
    }

    private string GetEvidenceFolderPath(string runId)
    {
        return Path.Combine(_workspaceBuilder.RepositoryRootPath, ".aos", "evidence", "runs", runId);
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_CreatesEvidenceFolderStructure()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "test input",
            CorrelationId = "corr-e2e-001"
        };

        var result = await _sut.ExecuteAsync(intent);

        // Verify orchestrator result
        result.IsSuccess.Should().BeTrue();
        result.RunId.Should().NotBeNullOrEmpty();
        result.FinalPhase.Should().Be("Roadmapper");

        // Verify evidence folder structure
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        Directory.Exists(evidenceFolder).Should().BeTrue("evidence folder should exist");

        // Verify run.json exists
        var runJsonPath = Path.Combine(evidenceFolder, "run.json");
        File.Exists(runJsonPath).Should().BeTrue("run.json should exist");

        // Verify logs directory exists
        var logsPath = Path.Combine(evidenceFolder, "logs");
        Directory.Exists(logsPath).Should().BeTrue("logs directory should exist");

        // Verify artifacts directory exists
        var artifactsPath = Path.Combine(evidenceFolder, "artifacts");
        Directory.Exists(artifactsPath).Should().BeTrue("artifacts directory should exist");

        // Verify commands.json exists after CloseRun
        var commandsJsonPath = Path.Combine(evidenceFolder, "commands.json");
        File.Exists(commandsJsonPath).Should().BeTrue("commands.json should exist");

        // Verify summary.json exists after CloseRun
        var summaryJsonPath = Path.Combine(evidenceFolder, "summary.json");
        File.Exists(summaryJsonPath).Should().BeTrue("summary.json should exist");
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_WritesInputJson()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "create roadmap for my app",
            InputNormalized = "spec roadmap",
            CorrelationId = "corr-e2e-002"
        };

        var result = await _sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();

        // Verify input.json was written
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        var inputJsonPath = Path.Combine(evidenceFolder, "input.json");
        File.Exists(inputJsonPath).Should().BeTrue("input.json should exist");

        // Verify input.json content
        var inputJson = File.ReadAllText(inputJsonPath);
        using var doc = JsonDocument.Parse(inputJson);
        var root = doc.RootElement;

        root.GetProperty("inputRaw").GetString().Should().Be("create roadmap for my app");
        root.GetProperty("inputNormalized").GetString().Should().Be("spec roadmap");
        root.GetProperty("correlationId").GetString().Should().Be("corr-e2e-002");
        root.GetProperty("runId").GetString().Should().Be(result.RunId);
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_RecordsCommandsInCommandsJson()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "test",
            CorrelationId = "corr-e2e-003"
        };

        var result = await _sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();

        // Verify commands.json content
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        var commandsJsonPath = Path.Combine(evidenceFolder, "commands.json");
        var commandsJson = File.ReadAllText(commandsJsonPath);
        using var doc = JsonDocument.Parse(commandsJson);
        var root = doc.RootElement;

        root.GetProperty("runId").GetString().Should().Be(result.RunId);

        var commands = root.GetProperty("commands");
        commands.GetArrayLength().Should().BeGreaterThan(0, "at least one command should be recorded");

        var firstCommand = commands[0];
        firstCommand.GetProperty("group").GetString().Should().Be("spec");
        firstCommand.GetProperty("command").GetString().Should().Be("roadmap");
        firstCommand.GetProperty("status").GetString().Should().BeOneOf("dispatched", "completed");
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_WritesSummaryJson()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "test",
            CorrelationId = "corr-e2e-004"
        };

        var result = await _sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();

        // Verify summary.json content
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        var summaryJsonPath = Path.Combine(evidenceFolder, "summary.json");
        var summaryJson = File.ReadAllText(summaryJsonPath);
        using var doc = JsonDocument.Parse(summaryJson);
        var root = doc.RootElement;

        root.GetProperty("runId").GetString().Should().Be(result.RunId);
        root.GetProperty("status").GetString().Should().Be("completed");

        // Verify outputs are present
        var outputs = root.GetProperty("outputs");
        outputs.GetProperty("targetPhase").GetString().Should().Be("Roadmapper");
        outputs.GetProperty("dispatchSuccess").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_WritesRunJsonWithMetadata()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "test",
            CorrelationId = "corr-e2e-005"
        };

        var result = await _sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();

        // Verify run.json content
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        var runJsonPath = Path.Combine(evidenceFolder, "run.json");
        var runJson = File.ReadAllText(runJsonPath);
        using var doc = JsonDocument.Parse(runJson);
        var root = doc.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("runId").GetString().Should().Be(result.RunId);
        root.GetProperty("status").GetString().Should().BeOneOf("completed", "failed");
        root.TryGetProperty("completedAt", out _).Should().BeTrue("completedAt should be present in final run.json");
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_UpdatesRunsIndex()
    {
        var intent = new WorkflowIntent
        {
            InputRaw = "test",
            CorrelationId = "corr-e2e-006"
        };

        var result = await _sut.ExecuteAsync(intent);

        result.IsSuccess.Should().BeTrue();

        // Verify runs index.json exists and contains the run
        var indexPath = Path.Combine(_workspaceBuilder.RepositoryRootPath, ".aos", "evidence", "runs", "index.json");
        File.Exists(indexPath).Should().BeTrue("runs index.json should exist");

        var indexJson = File.ReadAllText(indexPath);
        using var doc = JsonDocument.Parse(indexJson);
        var root = doc.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);

        var items = root.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0, "index should contain at least one run");

        // Find our run in the index
        var found = false;
        foreach (var item in items.EnumerateArray())
        {
            if (item.GetProperty("runId").GetString() == result.RunId)
            {
                found = true;
                item.GetProperty("status").GetString().Should().Be("completed");
                break;
            }
        }
        found.Should().BeTrue("run should be in the index");
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_FailedRun_WritesFailedSummary()
    {
        // Create a new test host with a failing roadmapper
        var workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description");
        var workspace = workspaceBuilder.Build();

        var testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        testHost.OverrideWithInstance<IWorkspace>(workspace);

        // Override the roadmapper to return failure (Roadmapper phase uses this directly)
        var mockRoadmapper = new Mock<IRoadmapper>();
        mockRoadmapper.Setup(x => x.GenerateRoadmapAsync(It.IsAny<RoadmapContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoadmapResult { IsSuccess = false, Error = "execution failed" });
        testHost.OverrideWithInstance(mockRoadmapper.Object);

        var sut = testHost.GetRequiredService<IOrchestrator>() as ExecutionOrchestrator
            ?? throw new InvalidOperationException("Could not resolve Orchestrator from DI");

        var intent = new WorkflowIntent
        {
            InputRaw = "test",
            CorrelationId = "corr-e2e-007"
        };

        var result = await sut.ExecuteAsync(intent);

        // Result should indicate failure
        result.IsSuccess.Should().BeFalse();

        // Verify summary.json shows failure
        var evidenceFolder = Path.Combine(workspaceBuilder.RepositoryRootPath, ".aos", "evidence", "runs", result.RunId!);
        var summaryJsonPath = Path.Combine(evidenceFolder, "summary.json");
        var summaryJson = File.ReadAllText(summaryJsonPath);
        using var doc = JsonDocument.Parse(summaryJson);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("failed");
        root.GetProperty("outputs").GetProperty("dispatchSuccess").GetBoolean().Should().BeFalse();

        // Verify run.json shows failure
        var runJsonPath = Path.Combine(evidenceFolder, "run.json");
        var runJson = File.ReadAllText(runJsonPath);
        using var runDoc = JsonDocument.Parse(runJson);
        var runRoot = runDoc.RootElement;
        runRoot.GetProperty("status").GetString().Should().Be("failed");

        // Cleanup
        testHost.Dispose();
        workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_MultipleRuns_MaintainsIndex()
    {
        var runIds = new List<string>();

        // Execute 3 runs
        for (int i = 0; i < 3; i++)
        {
            var intent = new WorkflowIntent
            {
                InputRaw = $"test run {i}",
                CorrelationId = $"corr-e2e-multi-{i}"
            };

            var result = await _sut.ExecuteAsync(intent);
            result.IsSuccess.Should().BeTrue();
            runIds.Add(result.RunId!);
        }

        // Verify all runs are in the index
        var indexPath = Path.Combine(_workspaceBuilder.RepositoryRootPath, ".aos", "evidence", "runs", "index.json");
        var indexJson = File.ReadAllText(indexPath);
        
        using var doc = JsonDocument.Parse(indexJson);
        var root = doc.RootElement;
        var items = root.GetProperty("items");

        items.GetArrayLength().Should().BeGreaterOrEqualTo(3, $"index should contain all runs. Found {items.GetArrayLength()} runs, expected 3.");

        // Verify each run is in the index
        var indexRunIds = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            indexRunIds.Add(item.GetProperty("runId").GetString()!);
        }

        foreach (var runId in runIds)
        {
            indexRunIds.Should().Contain(runId, $"run {runId} should be in index");
        }
    }
}

using System.Text.Json;
using FluentAssertions;
using ExecutionOrchestrator = nirmata.Agents.Execution.ControlPlane.Orchestrator;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Execution.Preflight.CommandSuggestion;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Tests.Fakes;
using nirmata.Agents.Tests.Fixtures;
using nirmata.Aos.Public;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Integration.Orchestrator;

/// <summary>
/// Integration tests for the confirmation flow in the orchestrator.
/// Verifies that the orchestrator properly handles user confirmation and cancellation.
/// </summary>
public class ConfirmationFlowIntegrationTests : IDisposable
{
    private readonly AosTestWorkspaceBuilder _workspaceBuilder;
    private readonly HandlerTestHost _testHost;
    private readonly FakeConfirmationGate _confirmationGate;
    private readonly AlwaysConfirmGateEvaluator _confirmationEvaluator;

    public ConfirmationFlowIntegrationTests()
    {
        // Create workspace builder with project for routing to phases that require confirmation
        _workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description");

        // Build the workspace to create the temp directory structure
        var workspace = _workspaceBuilder.Build();

        // Create test host with the workspace path
        _testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        _testHost.OverrideWithInstance<IWorkspace>(workspace);

        // Register a controllable confirmation gate and ignore prerequisite enforcement so tests drive the flow
        _confirmationGate = new FakeConfirmationGate();
        _confirmationEvaluator = new AlwaysConfirmGateEvaluator();
        _testHost.OverrideWithInstance<IConfirmationGate>(_confirmationGate);
        _testHost.OverrideWithInstance<IConfirmationGateEvaluator>(_confirmationEvaluator);
        _testHost.OverrideWithInstance<IPrerequisiteValidator>(new AlwaysReadyPrerequisiteValidator());
    }

/// <summary>
/// Simplified confirmation evaluator for tests that always requests confirmation
/// but immediately marks it as approved so the orchestrator flows deterministically.
/// </summary>
internal sealed class AlwaysConfirmGateEvaluator : IConfirmationGateEvaluator
{
    private readonly Dictionary<string, ConfirmationRequest> _pendingConfirmations = new();

    public ConfirmationEvaluationResult Evaluate(
        GatingResult gatingResult,
        GatingContext context,
        IntentClassificationResult classification)
    {
        var request = new ConfirmationRequest
        {
            OriginalInput = classification.ParsedCommand?.RawInput ?? "test-input",
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = classification.Intent.Kind.ToString(),
                Confidence = classification.Intent.Confidence,
                Reasoning = classification.Intent.Reasoning,
                UserInput = classification.ParsedCommand?.RawInput
            },
            ActionDescription = gatingResult.ProposedAction?.Description ?? "Test action",
            Confidence = classification.Intent.Confidence,
            Threshold = 0.0
        };

        _pendingConfirmations[request.Id] = request;

        return ConfirmationEvaluationResult.RequireConfirmation(
            request,
            classification.Intent.Confidence,
            0.0,
            gatingResult.ProposedAction?.RiskLevel ?? RiskLevel.WriteSafe,
            "Test evaluator always requires confirmation");
    }

    public bool EvaluateResponse(string confirmationId, ConfirmationResponse response)
    {
        if (!_pendingConfirmations.ContainsKey(confirmationId))
        {
            return false;
        }

        _pendingConfirmations.Remove(confirmationId);
        return response.Confirmed;
    }
}

    public void Dispose()
    {
        _testHost.Dispose();
        _workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WhenConfirmationRequiredAndUserRejects_ReturnsCancelledResult()
    {
        // Arrange
        _confirmationGate.SetRequireConfirmation(true);
        _confirmationGate.SetAutoConfirm(false); // Simulate user rejection
        _confirmationGate.Clear();

        var orchestrator = ResolveOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap for my app",
            CorrelationId = "corr-confirmation-reject"
        };

        // Act
        var result = await orchestrator.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalPhase.Should().Be("Cancelled");
        result.RunId.Should().NotBeNullOrEmpty();

        // Verify cancellation artifacts
        result.Artifacts.Should().ContainKey("cancellationReason");
        result.Artifacts["cancellationReason"].Should().Be("User rejected proposed action");
        result.Artifacts.Should().ContainKey("proposedPhase");
        result.Artifacts.Should().ContainKey("proposedAction");
        result.Artifacts.Should().ContainKey("confirmationRequestId");

        // Verify evidence folder shows cancelled run
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        var summaryJsonPath = Path.Combine(evidenceFolder, "summary.json");
        var summaryJson = File.ReadAllText(summaryJsonPath);
        using var doc = JsonDocument.Parse(summaryJson);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("failed");
        root.GetProperty("outputs").GetProperty("cancellationReason").GetString()
            .Should().Be("User rejected proposed action");
    }

    [Fact]
    public async Task ExecuteAsync_WhenConfirmationRequiredAndUserAccepts_ProceedsWithExecution()
    {
        // Arrange - Setup confirmation gate to require confirmation
        _confirmationGate.SetRequireConfirmation(true);
        _confirmationGate.SetAutoConfirm(true); // This would normally come from user input
        _confirmationGate.Clear();

        // We need to override the confirmation stack with one that simulates acceptance before building the provider
        var acceptingGate = new FakeConfirmationGateAccepts();
        OverrideConfirmationStack(acceptingGate, _confirmationEvaluator);

        var orchestrator = ResolveOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap for my app",
            CorrelationId = "corr-confirmation-accept"
        };

        // Act
        var result = await orchestrator.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().NotBe("Cancelled");
        result.RunId.Should().NotBeNullOrEmpty();

        // Verify evidence folder shows completed run
        var evidenceFolder = GetEvidenceFolderPath(result.RunId!);
        var summaryJsonPath = Path.Combine(evidenceFolder, "summary.json");
        var summaryJson = File.ReadAllText(summaryJsonPath);
        using var doc = JsonDocument.Parse(summaryJson);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoConfirmationRequired_SkipsConfirmationAndExecutes()
    {
        // Arrange
        _confirmationGate.SetRequireConfirmation(false);
        _confirmationGate.Clear();

        var orchestrator = ResolveOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run create roadmap for my app",
            CorrelationId = "corr-no-confirmation"
        };

        // Act
        var result = await orchestrator.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FinalPhase.Should().NotBeNullOrEmpty();
        result.FinalPhase.Should().NotBe("Cancelled");
        result.RunId.Should().NotBeNullOrEmpty();

        // Verify no cancellation artifacts
        result.Artifacts.Should().NotContainKey("cancellationReason");
    }

    [Fact]
    public async Task ExecuteAsync_CancellationWritesCorrectProposedActionDetails()
    {
        // Arrange
        _confirmationGate.SetRequireConfirmation(true);
        _confirmationGate.SetAutoConfirm(false);
        _confirmationGate.Clear();

        var orchestrator = ResolveOrchestrator();

        var intent = new WorkflowIntent
        {
            InputRaw = "/run test cancellation details",
            CorrelationId = "corr-cancellation-details"
        };

        // Act
        var result = await orchestrator.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FinalPhase.Should().Be("Cancelled");

        // Verify all expected artifacts are present with correct types
        result.Artifacts.Should().ContainKey("proposedPhase");
        result.Artifacts["proposedPhase"].Should().BeOfType<string>();

        result.Artifacts.Should().ContainKey("proposedAction");
        result.Artifacts["proposedAction"].Should().BeOfType<string>();

        result.Artifacts.Should().ContainKey("confirmationRequestId");
        result.Artifacts["confirmationRequestId"].Should().BeOfType<string>();

        result.Artifacts.Should().ContainKey("cancellationReason");
        result.Artifacts["cancellationReason"].Should().Be("User rejected proposed action");
    }

    private ExecutionOrchestrator ResolveOrchestrator()
        => _testHost.GetRequiredService<IOrchestrator>() as ExecutionOrchestrator
            ?? throw new InvalidOperationException("Could not resolve Orchestrator from DI");

    private void OverrideConfirmationStack(
        IConfirmationGate gate,
        IConfirmationGateEvaluator? evaluator = null)
    {
        // Swap out the confirmation gate (and optionally evaluator) before the service provider is built
        _testHost.OverrideWithInstance(gate);

        if (evaluator != null)
        {
            _testHost.OverrideWithInstance(evaluator);
        }
    }

    private string GetEvidenceFolderPath(string runId)
    {
        return Path.Combine(_workspaceBuilder.RepositoryRootPath, ".aos", "evidence", "runs", runId);
    }
}

/// <summary>
/// Prerequisite validator used by these tests to ensure runs always proceed to confirmation gating.
/// </summary>
internal sealed class AlwaysReadyPrerequisiteValidator : IPrerequisiteValidator
{
    public Task<PrerequisiteValidationResult> EnsureWorkspaceInitializedAsync(CancellationToken ct = default)
    {
        return Task.FromResult(PrerequisiteValidationResult.Satisfied("WorkspaceInitialization"));
    }

    public Task<PrerequisiteValidationResult> ValidateAsync(
        string targetPhase,
        GatingContext context,
        CancellationToken ct = default)
    {
        return Task.FromResult(PrerequisiteValidationResult.Satisfied(targetPhase));
    }

    public Task<WorkspaceBootstrapResult> CheckWorkspaceBootstrapAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new WorkspaceBootstrapResult
        {
            IsInitialized = true,
            HasAosDirectory = true,
            HasSpecDirectory = true,
            HasStateDirectory = true,
            FoundSpecFiles = Array.Empty<string>(),
            BootstrapCommand = "/init",
            BootstrapPrompt = "Workspace already initialized."
        });
    }
}

/// <summary>
/// Fake confirmation gate that always accepts confirmations.
/// Used to test the acceptance path of the confirmation flow.
/// </summary>
public sealed class FakeConfirmationGateAccepts : IConfirmationGate
{
    private readonly Dictionary<string, ConfirmationRequest> _pendingConfirmations = new();

    public ConfirmationGateResult Evaluate(IntentClassificationResult classification, ConfirmationGateOptions? options = null)
    {
        // Always require confirmation for testing
        var request = new ConfirmationRequest
        {
            OriginalInput = classification.ParsedCommand?.RawInput ?? "test-input",
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = classification.Intent.Kind.ToString(),
                Confidence = classification.Intent.Confidence,
                Reasoning = classification.Intent.Reasoning,
                UserInput = classification.ParsedCommand?.RawInput
            },
            ActionDescription = $"Execute {classification.Intent.Kind} action",
            Confidence = classification.Intent.Confidence,
            Threshold = options?.ConfirmationThreshold ?? 0.9
        };

        _pendingConfirmations[request.Id] = request;

        return ConfirmationGateResult.RequireConfirmation(request, classification.Intent.Confidence);
    }

    public ConfirmationGateResult EvaluateSuggestion(IntentClassificationResult classification, CommandProposal proposal, ConfirmationGateOptions? options = null)
    {
        var request = new ConfirmationRequest
        {
            OriginalInput = proposal.FormattedCommand ?? "suggested-command",
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = "SuggestedCommand",
                Confidence = proposal.Confidence,
                Reasoning = proposal.Reasoning,
                UserInput = proposal.FormattedCommand
            },
            ActionDescription = $"Execute suggested command: {proposal.CommandName}",
            Confidence = proposal.Confidence,
            Threshold = options?.ConfirmationThreshold ?? 0.9
        };

        _pendingConfirmations[request.Id] = request;

        return ConfirmationGateResult.RequireConfirmation(request, proposal.Confidence);
    }

    public bool ProcessResponse(ConfirmationResponse response)
    {
        if (!_pendingConfirmations.TryGetValue(response.RequestId, out var request))
        {
            return false;
        }

        _pendingConfirmations.Remove(response.RequestId);

        // Always accept (simulate user clicking "Accept")
        return true;
    }

    public PrerequisiteCheckResult EvaluatePrerequisites(string workspaceRoot)
    {
        return PrerequisiteCheckResult.Satisfied();
    }

    public ConfirmationGateResult EvaluateWithDestructiveness(
        IntentClassificationResult classification,
        string phase,
        GatingContext gatingContext,
        ConfirmationGateOptions? options = null)
    {
        return Evaluate(classification, options);
    }

    public RiskLevel? DetectGitOperationRisk(IReadOnlyList<string> arguments)
    {
        return null;
    }

    public RiskLevel? DetectFileSystemScopeRisk(IntentClassificationResult classification, string phase)
    {
        return null;
    }

    public ConfirmationRequest? GetPendingConfirmation(string id)
    {
        _pendingConfirmations.TryGetValue(id, out var request);
        return request;
    }
}

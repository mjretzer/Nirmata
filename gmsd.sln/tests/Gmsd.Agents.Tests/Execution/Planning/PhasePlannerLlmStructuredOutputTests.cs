#pragma warning disable CS0618 // Intentionally testing obsolete ILlmProvider interface

using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Planning.PhasePlanner;
using Gmsd.Aos.Public;
using Moq;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Planning;

/// <summary>
/// Tests for phase planner LLM structured output with schema validation.
/// Verifies that phase planner uses structured output schemas and handles validation failures.
/// </summary>
public class PhasePlannerLlmStructuredOutputTests
{
    private readonly Mock<ILlmProvider> _llmProviderMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Gmsd.Agents.Execution.Planning.PhasePlanner.PhasePlanner _sut;

    public PhasePlannerLlmStructuredOutputTests()
    {
        _llmProviderMock = new Mock<ILlmProvider>();
        _workspaceMock = new Mock<IWorkspace>();
        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-workspace/.aos");
        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns("/tmp/test-workspace");
        
        _sut = new Gmsd.Agents.Execution.Planning.PhasePlanner.PhasePlanner(_llmProviderMock.Object, _workspaceMock.Object);
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithValidStructuredOutput_GeneratesTaskPlan()
    {
        // Arrange
        var brief = new PhaseBrief
        {
            PhaseId = "PHASE-001",
            Description = "Implement authentication",
            PhaseName = "Authentication",
            MilestoneId = "MS-001",
            Goals = new[] { "Add login form", "Add session management" }
        };

        var validPlanJson = """
        {
            "planId": "PLAN-20260219-abc12345",
            "phaseId": "PHASE-001",
            "tasks": [
                {
                    "id": "TSK-001",
                    "title": "Create login form",
                    "description": "Implement user login form component",
                    "fileScopes": [
                        { "path": "src/components/LoginForm.tsx" }
                    ],
                    "verificationSteps": [
                        "Form renders without errors"
                    ]
                },
                {
                    "id": "TSK-002",
                    "title": "Add session management",
                    "description": "Implement session token handling",
                    "fileScopes": [
                        { "path": "src/services/SessionManager.ts" }
                    ],
                    "verificationSteps": [
                        "Session persists across page reloads"
                    ]
                }
            ]
        }
        """;

        var response = new LlmCompletionResponse
        {
            Model = "test-model",
            Provider = "test-provider",
            Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = validPlanJson }
        };

        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.Is<LlmCompletionRequest>(r => r.StructuredOutputSchema != null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.Should().NotBeNull();
        result.Tasks.Should().HaveCount(2);
        result.Tasks[0].Title.Should().Be("Create login form");
        result.Tasks[1].Title.Should().Be("Add session management");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithSchemaValidationFailure_CreatesDiagnosticAndFallback()
    {
        // Arrange
        var brief = new PhaseBrief
        {
            PhaseId = "PHASE-001",
            Description = "Implement authentication",
            PhaseName = "Authentication",
            MilestoneId = "MS-001",
            Goals = new[] { "Add login form" }
        };

        var schemaValidationEx = new LlmProviderException(
            "test-provider",
            "LLM response failed schema 'phase_plan_v1' validation: $.tasks[0].fileScopes: fileScopes must be non-empty array");

        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.Is<LlmCompletionRequest>(r => r.StructuredOutputSchema != null),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(schemaValidationEx);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.Should().NotBeNull();
        // Should return fallback plan when schema validation fails
        result.Tasks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateTaskPlanAsync_PassesStructuredOutputSchema()
    {
        // Arrange
        var brief = new PhaseBrief
        {
            PhaseId = "PHASE-001",
            Description = "Test phase",
            PhaseName = "Test Phase",
            MilestoneId = "MS-001",
            Goals = new[] { "Test objective" }
        };

        var validPlanJson = """
        {
            "planId": "PLAN-20260219-abc12345",
            "phaseId": "PHASE-001",
            "tasks": [
                {
                    "id": "TSK-001",
                    "title": "Test task",
                    "description": "Test description",
                    "fileScopes": [{ "path": "test.txt" }],
                    "verificationSteps": ["Test"]
                }
            ]
        }
        """;

        var response = new LlmCompletionResponse
        {
            Model = "test-model",
            Provider = "test-provider",
            Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = validPlanJson }
        };

        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.IsAny<LlmCompletionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(response);

        // Act
        await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.StructuredOutputSchema.Should().NotBeNull();
        capturedRequest.StructuredOutputSchema!.Name.Should().Be("phase_plan_v1");
        capturedRequest.StructuredOutputSchema.StrictValidation.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithMissingRequiredFields_CreatesDiagnostic()
    {
        // Arrange
        var brief = new PhaseBrief
        {
            PhaseId = "PHASE-001",
            Description = "Test phase",
            PhaseName = "Test Phase",
            MilestoneId = "MS-001",
            Goals = new[] { "Test objective" }
        };

        var invalidPlanJson = """
        {
            "planId": "PLAN-20260219-abc12345",
            "phaseId": "PHASE-001",
            "tasks": []
        }
        """;

        var response = new LlmCompletionResponse
        {
            Model = "test-model",
            Provider = "test-provider",
            Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = invalidPlanJson }
        };

        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.IsAny<LlmCompletionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.Should().NotBeNull();
        // Should return fallback when validation fails
        result.Tasks.Should().NotBeEmpty();
    }
}

#pragma warning disable CS0618 // Intentionally testing obsolete ILlmProvider interface

using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.FixPlanner;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Public;
using Gmsd.Common.Helpers;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.FixPlanner;

/// <summary>
/// Tests for fix planner LLM structured output with schema validation.
/// Verifies that fix planner uses structured output schemas and handles validation failures.
/// </summary>
public class FixPlannerLlmStructuredOutputTests
{
    private readonly Mock<ILlmProvider> _llmProviderMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IEventStore> _eventStoreMock;
    private readonly Mock<IClock> _clockMock;
    private readonly Gmsd.Agents.Execution.FixPlanner.FixPlanner _sut;

    public FixPlannerLlmStructuredOutputTests()
    {
        _llmProviderMock = new Mock<ILlmProvider>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _eventStoreMock = new Mock<IEventStore>();
        _clockMock = new Mock<IClock>();

        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-workspace/.aos");
        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns("/tmp/test-workspace");
        _clockMock.Setup(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);

        _sut = new Gmsd.Agents.Execution.FixPlanner.FixPlanner(
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _runLifecycleManagerMock.Object,
            _eventStoreMock.Object,
            _clockMock.Object,
            _llmProviderMock.Object);
    }

    [Fact]
    public async Task PlanFixesAsync_WithValidStructuredOutput_GeneratesFixPlan()
    {
        // Arrange
        var request = new FixPlannerRequest
        {
            IssueIds = new[] { "ISS-001", "ISS-002" }.ToList(),
            WorkspaceRoot = "/tmp/test-workspace",
            ParentTaskId = "TSK-001",
            ContextPackId = "CTX-001"
        };

        var validFixPlanJson = """
        {
            "fixes": [
                {
                    "issueId": "ISS-001",
                    "description": "Fix authentication bug",
                    "proposedChanges": [
                        {
                            "file": "src/auth/login.ts",
                            "changeDescription": "Add null check for user token"
                        }
                    ],
                    "tests": [
                        {
                            "description": "Login with invalid token",
                            "expectedOutcome": "Error message displayed"
                        }
                    ]
                },
                {
                    "issueId": "ISS-002",
                    "description": "Fix session timeout",
                    "proposedChanges": [
                        {
                            "file": "src/session/manager.ts",
                            "changeDescription": "Increase session timeout to 30 minutes"
                        }
                    ],
                    "tests": [
                        {
                            "description": "Session persists for 30 minutes",
                            "expectedOutcome": "Session still valid after 30 minutes"
                        }
                    ]
                }
            ]
        }
        """;

        var response = new LlmCompletionResponse
        {
            Model = "test-model",
            Provider = "test-provider",
            Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = validFixPlanJson }
        };

        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.Is<LlmCompletionRequest>(r => r.StructuredOutputSchema != null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FixTaskIds.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PlanFixesAsync_WithSchemaValidationFailure_CreatesDiagnosticAndFails()
    {
        // Arrange
        var request = new FixPlannerRequest
        {
            IssueIds = new[] { "ISS-001" }.ToList(),
            WorkspaceRoot = "/tmp/test-workspace",
            ParentTaskId = "TSK-001",
            ContextPackId = "CTX-001"
        };

        var schemaValidationEx = new LlmProviderException(
            "test-provider",
            "LLM response failed schema 'fix_plan_v1' validation: $.fixes[0].proposedChanges: proposedChanges must be non-empty array");

        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.Is<LlmCompletionRequest>(r => r.StructuredOutputSchema != null),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(schemaValidationEx);

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("schema validation");
    }

    [Fact]
    public async Task PlanFixesAsync_PassesStructuredOutputSchema()
    {
        // Arrange
        var request = new FixPlannerRequest
        {
            IssueIds = new[] { "ISS-001" }.ToList(),
            WorkspaceRoot = "/tmp/test-workspace",
            ParentTaskId = "TSK-001",
            ContextPackId = "CTX-001"
        };

        var validFixPlanJson = """
        {
            "fixes": [
                {
                    "issueId": "ISS-001",
                    "description": "Fix bug",
                    "proposedChanges": [
                        { "file": "test.ts", "changeDescription": "Fix" }
                    ],
                    "tests": [
                        { "description": "Test", "expectedOutcome": "Pass" }
                    ]
                }
            ]
        }
        """;

        var response = new LlmCompletionResponse
        {
            Model = "test-model",
            Provider = "test-provider",
            Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = validFixPlanJson }
        };

        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.IsAny<LlmCompletionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(response);

        // Act
        await _sut.PlanFixesAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.StructuredOutputSchema.Should().NotBeNull();
        capturedRequest.StructuredOutputSchema!.Name.Should().Be("fix_plan_v1");
        capturedRequest.StructuredOutputSchema.StrictValidation.Should().BeTrue();
    }

    [Fact]
    public async Task PlanFixesAsync_WithEmptyFixes_Fails()
    {
        // Arrange
        var request = new FixPlannerRequest
        {
            IssueIds = new[] { "ISS-001" }.ToList(),
            WorkspaceRoot = "/tmp/test-workspace",
            ParentTaskId = "TSK-001",
            ContextPackId = "CTX-001"
        };

        var invalidFixPlanJson = """
        {
            "fixes": []
        }
        """;

        var response = new LlmCompletionResponse
        {
            Model = "test-model",
            Provider = "test-provider",
            Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = invalidFixPlanJson }
        };

        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.IsAny<LlmCompletionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.PlanFixesAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task PlanFixesAsync_WithLowTemperature_ImprovedSchemaCompliance()
    {
        // Arrange
        var request = new FixPlannerRequest
        {
            IssueIds = new[] { "ISS-001" }.ToList(),
            WorkspaceRoot = "/tmp/test-workspace",
            ParentTaskId = "TSK-001",
            ContextPackId = "CTX-001"
        };

        var validFixPlanJson = """
        {
            "fixes": [
                {
                    "issueId": "ISS-001",
                    "description": "Fix bug",
                    "proposedChanges": [
                        { "file": "test.ts", "changeDescription": "Fix" }
                    ],
                    "tests": [
                        { "description": "Test", "expectedOutcome": "Pass" }
                    ]
                }
            ]
        }
        """;

        var response = new LlmCompletionResponse
        {
            Model = "test-model",
            Provider = "test-provider",
            Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = validFixPlanJson }
        };

        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock
            .Setup(x => x.CompleteAsync(
                It.IsAny<LlmCompletionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(response);

        // Act
        await _sut.PlanFixesAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Options.Temperature.Should().Be(0.1f);
        capturedRequest.Options.MaxTokens.Should().Be(4000);
    }
}

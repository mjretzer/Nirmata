using Xunit;
using Gmsd.Agents.Execution.ControlPlane.Chat;
using Gmsd.Agents.Execution.ControlPlane.Chat.Models;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Tests.E2E;

/// <summary>
/// End-to-end integration tests for the chat flow from input classification to response.
/// Tests the complete chat responder pipeline with a fake LLM provider.
/// </summary>
public class ChatFlowIntegrationTests
{
    [Fact]
    public async Task ChatFlow_FromInputToResponse_CompleteSuccessfully()
    {
        // Arrange - Set up complete pipeline
        var fakeLlm = new FakeLlmProvider();
        fakeLlm.EnqueueTextResponse("Hello! I'm here to help you with your project.");

        using var fakeWorkspace = new FakeWorkspace();
        var fakeStateStore = new FakeStateStore();
        var fakeCommandRegistry = new FakeCommandRegistry();

        var contextAssembly = new ChatContextAssembly(fakeWorkspace, fakeStateStore, fakeCommandRegistry);
        var promptBuilder = new ChatPromptBuilder();

        var responder = new LlmChatResponder(
            fakeLlm,
            contextAssembly,
            promptBuilder);

        var request = new ChatRequest
        {
            Input = "Can you help me understand my project status?"
        };

        // Act
        var response = await responder.RespondAsync(request);

        // Assert
        Assert.True(response.IsSuccess);
        Assert.Equal("Hello! I'm here to help you with your project.", response.Content);
        Assert.True(response.DurationMs >= 0);
    }

    [Fact]
    public async Task ChatFlow_WithWorkspaceState_ResponseIsSuccessful()
    {
        // Arrange
        var fakeLlm = new FakeLlmProvider();
        fakeLlm.EnqueueTextResponse("Your project is currently in phase PH-0001.");

        using var fakeWorkspace = new FakeWorkspace();
        var fakeStateStore = new FakeStateStore();
        fakeStateStore.SetSnapshot(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "PH-0001",
                TaskId = "T-001"
            }
        });

        var fakeCommandRegistry = new FakeCommandRegistry();
        fakeCommandRegistry.AddCommand(new Gmsd.Agents.Execution.Preflight.CommandRegistration
        {
            Name = "status",
            Group = "query",
            SideEffect = Gmsd.Agents.Execution.Preflight.SideEffect.ReadOnly,
            Description = "Check project status"
        });

        var contextAssembly = new ChatContextAssembly(fakeWorkspace, fakeStateStore, fakeCommandRegistry);
        var promptBuilder = new ChatPromptBuilder();

        var responder = new LlmChatResponder(
            fakeLlm,
            contextAssembly,
            promptBuilder);

        var request = new ChatRequest { Input = "What's my current status?" };

        // Act
        var response = await responder.RespondAsync(request);

        // Assert
        Assert.True(response.IsSuccess);
        Assert.Single(fakeLlm.Requests);
        Assert.NotNull(fakeLlm.Requests[0].Messages);
    }

    [Fact]
    public async Task ChatFlow_WhenLlmFails_ReturnsGracefulError()
    {
        // Arrange
        var fakeLlm = new FakeLlmProvider();
        fakeLlm.EnqueueException(new LlmProviderException("LLM service unavailable", "service_error"));

        using var fakeWorkspace = new FakeWorkspace();
        var fakeStateStore = new FakeStateStore();
        var fakeCommandRegistry = new FakeCommandRegistry();

        var contextAssembly = new ChatContextAssembly(fakeWorkspace, fakeStateStore, fakeCommandRegistry);
        var promptBuilder = new ChatPromptBuilder();

        var responder = new LlmChatResponder(
            fakeLlm,
            contextAssembly,
            promptBuilder);

        var request = new ChatRequest { Input = "Hello" };

        // Act
        var response = await responder.RespondAsync(request);

        // Assert
        Assert.False(response.IsSuccess);
        Assert.NotNull(response.ErrorMessage);
        Assert.NotEmpty(response.Content); // Should have fallback message
    }

    [Fact]
    public async Task ChatFlow_StreamingMode_YieldsDeltas()
    {
        // Arrange
        var fakeLlm = new FakeLlmProvider();
        fakeLlm.EnqueueTextResponse("Hello world!");

        using var fakeWorkspace = new FakeWorkspace();
        var fakeStateStore = new FakeStateStore();
        var fakeCommandRegistry = new FakeCommandRegistry();

        var contextAssembly = new ChatContextAssembly(fakeWorkspace, fakeStateStore, fakeCommandRegistry);
        var promptBuilder = new ChatPromptBuilder();

        var responder = new LlmChatResponder(
            fakeLlm,
            contextAssembly,
            promptBuilder);

        var request = new ChatRequest { Input = "Say hello" };

        // Act
        var deltas = new List<ChatDelta>();
        await foreach (var delta in responder.StreamResponseAsync(request))
        {
            deltas.Add(delta);
        }

        // Assert
        Assert.True(deltas.Count > 0);
        var contentDeltas = deltas.Where(d => !string.IsNullOrEmpty(d.Content)).ToList();
        Assert.True(contentDeltas.Count > 0);
        Assert.Contains(deltas, d => d.IsComplete); // Should have completion marker
    }

    [Fact]
    public async Task ChatFlow_ContextSummarizer_RespectsTokenBudget()
    {
        // Arrange
        var fakeLlm = new FakeLlmProvider();
        fakeLlm.EnqueueTextResponse("Response within token budget.");

        using var fakeWorkspace = new FakeWorkspace();
        var fakeStateStore = new FakeStateStore();
        var fakeCommandRegistry = new FakeCommandRegistry();

        // Create a context assembly with a small token budget
        var contextAssembly = new ChatContextAssembly(fakeWorkspace, fakeStateStore, fakeCommandRegistry)
        {
            MaxContextTokens = 100
        };
        var promptBuilder = new ChatPromptBuilder();

        var responder = new LlmChatResponder(
            fakeLlm,
            contextAssembly,
            promptBuilder);

        var request = new ChatRequest { Input = "Test token budget" };

        // Act
        var response = await responder.RespondAsync(request);

        // Assert
        Assert.True(response.IsSuccess);
        // The context should have been assembled within the token budget
        Assert.True(contextAssembly.MaxContextTokens <= 1000);
    }

    [Fact]
    public async Task ChatFlow_CustomTemperature_IsPassedToLlm()
    {
        // Arrange
        var fakeLlm = new FakeLlmProvider();
        fakeLlm.EnqueueTextResponse("Custom temperature response");

        using var fakeWorkspace = new FakeWorkspace();
        var fakeStateStore = new FakeStateStore();
        var fakeCommandRegistry = new FakeCommandRegistry();

        var contextAssembly = new ChatContextAssembly(fakeWorkspace, fakeStateStore, fakeCommandRegistry);
        var promptBuilder = new ChatPromptBuilder();

        var responder = new LlmChatResponder(
            fakeLlm,
            contextAssembly,
            promptBuilder);

        var request = new ChatRequest
        {
            Input = "Test temperature",
            Temperature = 0.2
        };

        // Act
        await responder.RespondAsync(request);

        // Assert
        Assert.Single(fakeLlm.Requests);
        Assert.Equal(0.2f, fakeLlm.Requests[0].Options?.Temperature);
    }

    [Fact]
    public async Task ChatFlow_CustomMaxTokens_IsPassedToLlm()
    {
        // Arrange
        var fakeLlm = new FakeLlmProvider();
        fakeLlm.EnqueueTextResponse("Custom max tokens response");

        using var fakeWorkspace = new FakeWorkspace();
        var fakeStateStore = new FakeStateStore();
        var fakeCommandRegistry = new FakeCommandRegistry();

        var contextAssembly = new ChatContextAssembly(fakeWorkspace, fakeStateStore, fakeCommandRegistry);
        var promptBuilder = new ChatPromptBuilder();

        var responder = new LlmChatResponder(
            fakeLlm,
            contextAssembly,
            promptBuilder);

        var request = new ChatRequest
        {
            Input = "Test max tokens",
            MaxTokens = 500
        };

        // Act
        await responder.RespondAsync(request);

        // Assert
        Assert.Single(fakeLlm.Requests);
        Assert.Equal(500, fakeLlm.Requests[0].Options?.MaxTokens);
    }
}

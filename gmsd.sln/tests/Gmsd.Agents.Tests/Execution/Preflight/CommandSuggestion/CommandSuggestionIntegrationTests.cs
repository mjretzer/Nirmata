using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Execution.Preflight.CommandSuggestion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Preflight.CommandSuggestion;

/// <summary>
/// Integration tests for the command suggestion flow.
/// Tests the full flow: freeform input -> suggestion -> confirmation/rejection.
/// </summary>
public class CommandSuggestionIntegrationTests
{
    private readonly LlmCommandSuggester _suggester;
    private readonly ConfirmationGate _confirmationGate;
    private readonly CommandSuggestionOptions _options;
    private readonly FakeLlmProvider _llmProvider;
    private readonly FakeCommandRegistry _commandRegistry;

    public CommandSuggestionIntegrationTests()
    {
        _llmProvider = new FakeLlmProvider();
        _commandRegistry = new FakeCommandRegistry();
        _options = new CommandSuggestionOptions
        {
            EnableSuggestionMode = true,
            ConfidenceThreshold = 0.7,
            MaxInputLength = 1000
        };

        _suggester = new LlmCommandSuggester(
            _llmProvider,
            _commandRegistry,
            Options.Create(_options),
            NullLogger<LlmCommandSuggester>.Instance);

        _confirmationGate = new ConfirmationGate();

        // Register known commands
        _commandRegistry.RegisterCommand("plan", "Create or update a plan", SideEffect.Write);
        _commandRegistry.RegisterCommand("run", "Execute the current task", SideEffect.Write);
        _commandRegistry.RegisterCommand("status", "Check current status", SideEffect.ReadOnly);
    }

    [Fact]
    public async Task FreeformInput_PlanPhaseSuggestion_EmitsProposal()
    {
        // Arrange: Setup LLM to return a structured plan suggestion
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""Plan the foundation phase"",
                ""parameters"": {
                    ""phase-id"": ""PH-0001""
                }
            },
            ""command"": ""/plan"",
            ""group"": ""workflow"",
            ""rationale"": ""The user explicitly asked to plan the foundation phase."",
            ""expectedOutcome"": ""A plan command can be confirmed and executed.""
        }");

        // Act: Send freeform input
        var input = "plan the foundation phase";
        var proposal = await _suggester.SuggestAsync(input);

        // Assert: Should get a valid proposal
        Assert.NotNull(proposal);
        Assert.Equal("plan", proposal!.CommandName);
        Assert.Equal(1.0, proposal.Confidence);
        Assert.Contains("--phase-id", proposal.Arguments);
    }

    [Fact]
    public async Task Suggestion_UserConfirms_ExecutesCommand()
    {
        // Arrange: Create a classification with suggestion
        var proposal = new CommandProposal
        {
            CommandName = "plan",
            Arguments = new[] { "--phase-id", "PH-0001" },
            Confidence = 0.85,
            Reasoning = "User wants to plan the foundation phase",
            FormattedCommand = "/plan --phase-id PH-0001"
        };

        var classification = IntentClassificationResult.Suggestion(
            new Intent
            {
                Kind = IntentKind.WorkflowCommand,
                SideEffect = SideEffect.Write,
                Confidence = 0.85,
                Reasoning = "Suggested command: /plan --phase-id PH-0001"
            },
            proposal);

        // Act: Evaluate in confirmation gate
        var result = _confirmationGate.Evaluate(classification);

        // Assert: Should require confirmation
        Assert.False(result.CanProceed);
        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(result.Request);

        // Act: Confirm the suggestion
        var response = new ConfirmationResponse
        {
            RequestId = result.Request!.Id,
            Confirmed = true
        };
        var confirmed = _confirmationGate.ProcessResponse(response);

        // Assert: Should proceed after confirmation
        Assert.True(confirmed);
    }

    [Fact]
    public async Task Suggestion_UserRejects_ContinuesAsChat()
    {
        // Arrange: Create a classification with suggestion
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Arguments = Array.Empty<string>(),
            Confidence = 0.75,
            Reasoning = "User wants to execute",
            FormattedCommand = "/run"
        };

        var classification = IntentClassificationResult.Suggestion(
            new Intent
            {
                Kind = IntentKind.WorkflowCommand,
                SideEffect = SideEffect.Write,
                Confidence = 0.75,
                Reasoning = "Suggested command: /run"
            },
            proposal);

        // Act: Evaluate in confirmation gate
        var result = _confirmationGate.Evaluate(classification);

        // Assert: Should require confirmation
        Assert.True(result.RequiresConfirmation);

        // Act: Reject the suggestion
        var response = new ConfirmationResponse
        {
            RequestId = result.Request!.Id,
            Confirmed = false
        };
        var confirmed = _confirmationGate.ProcessResponse(response);

        // Assert: Should not proceed after rejection
        Assert.False(confirmed);
    }

    [Fact]
    public async Task AmbiguousInput_NoSuggestion_FallsBackToChat()
    {
        // Arrange: Setup LLM to return explicit structured no-op
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""no-op""
            },
            ""command"": ""/status"",
            ""group"": ""chat"",
            ""rationale"": ""No command should be suggested for this conversational input."",
            ""expectedOutcome"": ""System stays in chat mode and does not execute a command.""
        }");

        // Act: Send ambiguous input
        var input = "what's the weather like today?";
        var proposal = await _suggester.SuggestAsync(input);

        // Assert: Should get null (no suggestion)
        Assert.Null(proposal);
    }

    [Fact]
    public async Task LowConfidenceSuggestion_NotEmitted()
    {
        // Arrange: Setup LLM to return an invalid structured payload (schema mismatch)
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""Plan""
            },
            ""command"": ""plan"",
            ""group"": ""workflow"",
            ""rationale"": ""short"",
            ""expectedOutcome"": ""short""
        }");

        // Act: Send input
        var input = "something about planning maybe";
        var proposal = await _suggester.SuggestAsync(input);

        // Assert: Should get null due schema validation failure
        Assert.Null(proposal);
    }

    [Fact]
    public async Task LegacyPayload_FallbackParser_StillSupported()
    {
        // Arrange: Legacy response remains accepted for backward compatibility
        _llmProvider.SetupResponse(@"{
            ""suggested"": true,
            ""command"": ""run"",
            ""arguments"": [""--target"", ""unit""],
            ""confidence"": 0.8,
            ""reasoning"": ""Legacy suggester payload"",
            ""formatted"": ""/run --target unit""
        }");

        // Act
        var proposal = await _suggester.SuggestAsync("run unit tests");

        // Assert
        Assert.NotNull(proposal);
        Assert.Equal("run", proposal!.CommandName);
        Assert.Equal(new[] { "--target", "unit" }, proposal.Arguments);
    }

    [Fact]
    public void IntentClassificationResult_HasSuggestion_WithValidProposal_ReturnsTrue()
    {
        // Arrange
        var proposal = new CommandProposal
        {
            CommandName = "status",
            Confidence = 0.9
        };

        var result = IntentClassificationResult.Suggestion(
            new Intent
            {
                Kind = IntentKind.Status,
                SideEffect = SideEffect.ReadOnly,
                Confidence = 0.9,
                Reasoning = "Suggested command: /status"
            },
            proposal);

        // Act & Assert
        Assert.True(result.HasSuggestion());
        Assert.Equal("llm", result.SuggestionSource);
    }

    [Fact]
    public void IntentClassificationResult_HasSuggestion_WithoutProposal_ReturnsFalse()
    {
        // Arrange
        var result = IntentClassificationResult.Chat(
            new Intent
            {
                Kind = IntentKind.Unknown,
                SideEffect = SideEffect.None,
                Confidence = 0.9,
                Reasoning = "Just chatting"
            });

        // Act & Assert
        Assert.False(result.HasSuggestion());
    }

    #region Fakes

    private class FakeLlmProvider : ILlmProvider
    {
        private string _response = "{\"suggested\": false}";

        public string ProviderName => "Fake";

        public void SetupResponse(string response)
        {
            _response = response;
        }

        public Task<LlmCompletionResponse> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = _response
                },
                Model = "fake-model",
                Provider = "fake-provider"
            });
        }

        public IAsyncEnumerable<LlmDelta> StreamCompletionAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<LlmDelta>();
        }
    }

    private class FakeCommandRegistry : ICommandRegistry
    {
        private readonly Dictionary<string, CommandRegistration> _commands = new();

        public void RegisterCommand(string name, string description, SideEffect sideEffect)
        {
            _commands[name] = new CommandRegistration
            {
                Name = name,
                Group = "test",
                Description = description,
                SideEffect = sideEffect
            };
        }

        public IEnumerable<CommandRegistration> GetAllCommands()
        {
            return _commands.Values;
        }

        public bool IsKnownCommand(string name)
        {
            return _commands.ContainsKey(name);
        }

        public CommandRegistration? GetCommand(string name)
        {
            _commands.TryGetValue(name, out var cmd);
            return cmd;
        }

        public IEnumerable<CommandRegistration> GetCommandsBySideEffect(SideEffect sideEffect)
        {
            return _commands.Values.Where(c => c.SideEffect == sideEffect);
        }

        public IEnumerable<string> GetSuggestions(string unknownCommand, int maxSuggestions = 3)
        {
            return Array.Empty<string>();
        }
    }

    #endregion
}

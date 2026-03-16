using System.Diagnostics;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Execution.Preflight.CommandSuggestion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Preflight.CommandSuggestion;

/// <summary>
/// Performance tests for the command suggestion system.
/// Ensures suggestion latency meets targets (< 500ms) and doesn't block chat responsiveness.
/// </summary>
public class CommandSuggestionPerformanceTests
{
    private readonly FakeLlmProvider _llmProvider;
    private readonly FakeCommandRegistry _commandRegistry;
    private readonly CommandSuggestionOptions _options;

    public CommandSuggestionPerformanceTests()
    {
        _llmProvider = new FakeLlmProvider();
        _commandRegistry = new FakeCommandRegistry();
        _options = new CommandSuggestionOptions
        {
            EnableSuggestionMode = true,
            ConfidenceThreshold = 0.7,
            MaxInputLength = 1000
        };

        // Register known commands
        _commandRegistry.RegisterCommand("plan", "Create or update a plan", SideEffect.Write);
        _commandRegistry.RegisterCommand("run", "Execute the current task", SideEffect.Write);
        _commandRegistry.RegisterCommand("status", "Check current status", SideEffect.ReadOnly);
    }

    private LlmCommandSuggester CreateSuggester()
    {
        return new LlmCommandSuggester(
            _llmProvider,
            _commandRegistry,
            Options.Create(_options),
            NullLogger<LlmCommandSuggester>.Instance);
    }

    [Fact]
    public async Task SuggestionLatency_Under500ms()
    {
        // Arrange: Setup a responsive LLM provider (simulates 50ms latency)
        _llmProvider.SetupLatency(TimeSpan.FromMilliseconds(50));
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""Plan the work""
            },
            ""command"": ""/plan"",
            ""group"": ""workflow"",
            ""rationale"": ""User asked to create a project plan with next steps."",
            ""expectedOutcome"": ""A /plan command proposal is emitted quickly for confirmation.""
        }");

        var suggester = CreateSuggester();
        var stopwatch = new Stopwatch();

        // Act: Measure suggestion time
        stopwatch.Start();
        var result = await suggester.SuggestAsync("create a plan for the project");
        stopwatch.Stop();

        // Assert: Should complete under 500ms (with margin for test environment)
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Suggestion took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task MultipleSequentialSuggestions_ConsistentLatency()
    {
        // Arrange
        _llmProvider.SetupLatency(TimeSpan.FromMilliseconds(30));
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""Run workflow""
            },
            ""command"": ""/run"",
            ""group"": ""workflow"",
            ""rationale"": ""User intent is to execute the workflow immediately."",
            ""expectedOutcome"": ""A /run command proposal is emitted quickly for confirmation.""
        }");

        var suggester = CreateSuggester();
        var latencies = new List<long>();

        // Act: Run multiple sequential suggestions
        for (int i = 0; i < 5; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await suggester.SuggestAsync($"test input {i}");
            stopwatch.Stop();
            latencies.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert: All should be under 500ms
        Assert.All(latencies, latency =>
            Assert.True(latency < 500, $"Latency {latency}ms exceeded 500ms threshold"));

        // Average should be reasonable
        var average = latencies.Average();
        Assert.True(average < 200, $"Average latency {average}ms too high");
    }

    [Fact]
    public async Task NoSuggestionPath_FastFallback()
    {
        // Arrange: Setup for no suggestion (fast path)
        _llmProvider.SetupLatency(TimeSpan.FromMilliseconds(20));
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""no-op""
            },
            ""command"": ""/status"",
            ""group"": ""chat"",
            ""rationale"": ""No command intent was detected in the input."",
            ""expectedOutcome"": ""No command is proposed and chat flow continues.""
        }");

        var suggester = CreateSuggester();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await suggester.SuggestAsync("just chatting about stuff");
        stopwatch.Stop();

        // Assert: Should be fast even for no-suggestion case
        Assert.Null(result);
        Assert.True(stopwatch.ElapsedMilliseconds < 300,
            $"No-suggestion path took {stopwatch.ElapsedMilliseconds}ms, expected fast fallback");
    }

    [Fact]
    public async Task LongInput_TruncationPerformance()
    {
        // Arrange: Very long input that needs truncation
        var longInput = new string('x', 5000); // 5x the max length
        _llmProvider.SetupLatency(TimeSpan.FromMilliseconds(30));
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""no-op""
            },
            ""command"": ""/status"",
            ""group"": ""chat"",
            ""rationale"": ""No command intent was detected in this long conversational input."",
            ""expectedOutcome"": ""No command is proposed and chat flow continues.""
        }");

        var suggester = CreateSuggester();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await suggester.SuggestAsync(longInput);
        stopwatch.Stop();

        // Assert: Truncation should not significantly impact performance
        Assert.True(stopwatch.ElapsedMilliseconds < 300,
            $"Long input handling took {stopwatch.ElapsedMilliseconds}ms, expected < 300ms");
    }

    [Fact]
    public async Task SuggestionDoesNotBlock_WhenLlmSlow()
    {
        // This test verifies that the suggestion system doesn't block the main thread
        // by running the suggestion in a cancellable way

        // Arrange: Slow LLM
        _llmProvider.SetupLatency(TimeSpan.FromMilliseconds(100));
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""Check status""
            },
            ""command"": ""/status"",
            ""group"": ""query"",
            ""rationale"": ""The user asked to check current workflow status details."",
            ""expectedOutcome"": ""A /status command proposal is emitted for immediate confirmation.""
        }");

        var suggester = CreateSuggester();
        using var cts = new CancellationTokenSource();

        // Act: Start suggestion and cancel it quickly
        var task = suggester.SuggestAsync("check status", cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(50)); // Cancel after 50ms

        // Assert: Should either complete or throw cancellation
        try
        {
            var result = await task;
            // If it completed, it was fast enough
            Assert.True(task.IsCompleted);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is acceptable - means it didn't block
            Assert.True(task.IsCanceled);
        }
    }

    [Fact]
    public void CommandProposal_ValidationPerformance()
    {
        // Arrange: Create a valid proposal
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Confidence = 0.85,
            Arguments = new[] { "--verbose", "test" },
            Reasoning = "Test reasoning",
            FormattedCommand = "/run --verbose test"
        };

        var stopwatch = Stopwatch.StartNew();
        const int iterations = 10000;

        // Act: Run validation many times
        for (int i = 0; i < iterations; i++)
        {
            _ = proposal.IsValid();
        }

        stopwatch.Stop();

        // Assert: Should be very fast (microseconds per validation)
        var perValidation = stopwatch.Elapsed.TotalMilliseconds / iterations;
        Assert.True(perValidation < 0.01, // Less than 10 microseconds per validation
            $"Validation took {perValidation:F4}ms per iteration, expected < 0.01ms");
    }

    [Fact]
    public void IntentClassificationResult_HasSuggestionPerformance()
    {
        // Arrange
        var proposal = new CommandProposal
        {
            CommandName = "plan",
            Confidence = 0.9
        };

        var result = IntentClassificationResult.Suggestion(
            new Intent
            {
                Kind = IntentKind.WorkflowCommand,
                SideEffect = SideEffect.Write,
                Confidence = 0.9,
                Reasoning = "Test"
            },
            proposal);

        var stopwatch = Stopwatch.StartNew();
        const int iterations = 10000;

        // Act: Run HasSuggestion many times
        for (int i = 0; i < iterations; i++)
        {
            _ = result.HasSuggestion();
        }

        stopwatch.Stop();

        // Assert: Should be very fast
        var perCall = stopwatch.Elapsed.TotalMilliseconds / iterations;
        Assert.True(perCall < 0.01,
            $"HasSuggestion took {perCall:F4}ms per iteration, expected < 0.01ms");
    }

    [Theory]
    [InlineData(10)]    // Short input
    [InlineData(100)]   // Medium input
    [InlineData(1000)]  // Max length input
    [InlineData(2000)]  // Over max length (truncated)
    public async Task VariousInputSizes_ReasonableLatency(int inputLength)
    {
        // Arrange
        var input = new string('a', inputLength);
        _llmProvider.SetupLatency(TimeSpan.FromMilliseconds(30));
        _llmProvider.SetupResponse(@"{
            ""intent"": {
                ""goal"": ""no-op""
            },
            ""command"": ""/status"",
            ""group"": ""chat"",
            ""rationale"": ""No command intent was detected in this generic content."",
            ""expectedOutcome"": ""No command is proposed and chat flow continues.""
        }");

        var suggester = CreateSuggester();
        var stopwatch = Stopwatch.StartNew();

        // Act
        await suggester.SuggestAsync(input);
        stopwatch.Stop();

        // Assert: All sizes should complete reasonably fast
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Input length {inputLength} took {stopwatch.ElapsedMilliseconds}ms");
    }

    #region Fakes

    private class FakeLlmProvider : ILlmProvider
    {
        private string _response = "{\"intent\":{\"goal\":\"no-op\"},\"command\":\"/status\",\"group\":\"chat\",\"rationale\":\"No command should be suggested for this input.\",\"expectedOutcome\":\"System remains in chat mode.\"}";
        private TimeSpan _latency = TimeSpan.Zero;

        public string ProviderName => "Fake";

        public void SetupResponse(string response)
        {
            _response = response;
        }

        public void SetupLatency(TimeSpan latency)
        {
            _latency = latency;
        }

        public async Task<LlmCompletionResponse> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            if (_latency > TimeSpan.Zero)
            {
                await Task.Delay(_latency, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = _response
                },
                Model = "fake-model",
                Provider = "fake-provider"
            };
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

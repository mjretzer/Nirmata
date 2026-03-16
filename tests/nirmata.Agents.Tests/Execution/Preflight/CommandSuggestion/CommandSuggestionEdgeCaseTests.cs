using System.Text.Json;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Execution.Preflight.CommandSuggestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Preflight.CommandSuggestion;

/// <summary>
/// Edge case tests for the command suggestion system.
/// Tests error handling, graceful fallbacks, and boundary conditions.
/// </summary>
public class CommandSuggestionEdgeCaseTests
{
    private readonly Mock<ILlmProvider> _llmProviderMock;
    private readonly Mock<ICommandRegistry> _commandRegistryMock;
    private readonly Mock<ILogger<LlmCommandSuggester>> _loggerMock;
    private readonly CommandSuggestionOptions _options;

    public CommandSuggestionEdgeCaseTests()
    {
        _llmProviderMock = new Mock<ILlmProvider>();
        _commandRegistryMock = new Mock<ICommandRegistry>();
        _loggerMock = new Mock<ILogger<LlmCommandSuggester>>();
        _options = new CommandSuggestionOptions
        {
            EnableSuggestionMode = true,
            ConfidenceThreshold = 0.7,
            MaxInputLength = 1000
        };

        // Setup default command registry behavior
        _commandRegistryMock.Setup(r => r.GetAllCommands()).Returns(new List<CommandRegistration>());
        _commandRegistryMock.Setup(r => r.IsKnownCommand(It.IsAny<string>())).Returns(true);
    }

    private LlmCommandSuggester CreateSuggester()
    {
        return new LlmCommandSuggester(
            _llmProviderMock.Object,
            _commandRegistryMock.Object,
            Options.Create(_options),
            _loggerMock.Object);
    }

    [Fact]
    public async Task MalformedJsonResponse_GracefulFallback_ReturnsNull()
    {
        // Arrange: Setup LLM to return invalid JSON
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = "not valid json at all {{["
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("plan something");

        // Assert: Should return null gracefully
        Assert.Null(result);

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to parse")),
                It.IsAny<JsonException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LlmUnavailable_GracefulFallback_ReturnsNull()
    {
        // Arrange: Setup LLM to throw exception
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("run the tests");

        // Assert: Should return null gracefully
        Assert.Null(result);

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to get command suggestion")),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task EmptyLlmResponse_GracefulFallback_ReturnsNull()
    {
        // Arrange: Setup LLM to return empty response
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = ""
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("do something");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task NullLlmResponse_GracefulFallback_ReturnsNull()
    {
        // Arrange: Setup LLM to return null content
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = null!
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("check status");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SuggestedFalseResponse_ReturnsNull()
    {
        // Arrange: LLM returns suggested: false
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = "{\"suggested\": false}"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("hello how are you");

        // Assert: Should return null (no suggestion)
        Assert.Null(result);
    }

    [Fact]
    public async Task UnknownCommandInProposal_Rejected_ReturnsNull()
    {
        // Arrange: Setup registry to reject unknown commands
        _commandRegistryMock.Setup(r => r.GetAllCommands())
            .Returns(new List<CommandRegistration>
            {
                new() { Name = "plan", Description = "Plan", Group = "workflow", SideEffect = SideEffect.Write },
                new() { Name = "run", Description = "Run", Group = "workflow", SideEffect = SideEffect.Write }
            });
        _commandRegistryMock.Setup(r => r.IsKnownCommand("unknown")).Returns(false);

        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = @"{
                        ""suggested"": true,
                        ""command"": ""unknown"",
                        ""arguments"": [],
                        ""confidence"": 0.9,
                        ""reasoning"": ""Try unknown command"",
                        ""formatted"": ""/unknown""
                    }"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("do something weird");

        // Assert: Should reject unknown command
        Assert.Null(result);

        // Verify warning was logged about unknown command
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unknown command")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidArgumentsInProposal_ReturnsNull()
    {
        // Arrange: LLM returns malformed arguments structure (would cause parsing issues)
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = @"{
                        ""suggested"": true,
                        ""command"": ""plan"",
                        ""arguments"": null,
                        ""confidence"": 0.9,
                        ""reasoning"": ""Valid suggestion"",
                        ""formatted"": ""/plan""
                    }"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("make a plan");

        // Assert: Should handle null arguments gracefully
        Assert.NotNull(result);
        Assert.Equal("plan", result!.CommandName);
        Assert.NotNull(result.Arguments);
    }

    [Fact]
    public async Task ConfidenceAtBoundary_ReturnsProposal()
    {
        // Arrange: Confidence exactly at threshold
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = @"{
                        ""suggested"": true,
                        ""command"": ""run"",
                        ""arguments"": [],
                        ""confidence"": 0.7,
                        ""reasoning"": ""At threshold"",
                        ""formatted"": ""/run""
                    }"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("execute now");

        // Assert: Should include proposal (confidence >= threshold)
        Assert.NotNull(result);
        Assert.Equal(0.7, result!.Confidence);
    }

    [Fact]
    public async Task ConfidenceJustBelowBoundary_ReturnsNull()
    {
        // Arrange: Confidence just below threshold
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = @"{
                        ""suggested"": true,
                        ""command"": ""run"",
                        ""arguments"": [],
                        ""confidence"": 0.699,
                        ""reasoning"": ""Just below"",
                        ""formatted"": ""/run""
                    }"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("execute now");

        // Assert: Should reject proposal (confidence < threshold)
        Assert.Null(result);
    }

    [Fact]
    public async Task EmptyInput_ReturnsNull()
    {
        // Arrange
        var suggester = CreateSuggester();

        // Act: Test various empty inputs
        var emptyResult = await suggester.SuggestAsync("");
        var whitespaceResult = await suggester.SuggestAsync("   ");
        var nullResult = await suggester.SuggestAsync(null!);

        // Assert
        Assert.Null(emptyResult);
        Assert.Null(whitespaceResult);
        Assert.Null(nullResult);
    }

    [Fact]
    public async Task VeryLongInput_TruncatedAndProcessed()
    {
        // Arrange: Create input longer than MaxInputLength
        var longInput = new string('x', 1500);
        var capturedInput = "";

        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((req, _) =>
            {
                // Capture the user message content
                var userMsg = req.Messages.FirstOrDefault(m => m.Role == LlmMessageRole.User);
                if (userMsg != null && userMsg.Content != null)
                {
                    // Extract input from the user prompt format
                    var content = userMsg.Content;
                    var start = content.IndexOf("\"\"\"") + 3;
                    var end = content.LastIndexOf("\"\"\"");
                    if (start > 2 && end > start)
                    {
                        capturedInput = content[start..end];
                    }
                }
            })
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = "{\"suggested\": false}"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        await suggester.SuggestAsync(longInput);

        // Assert: Input should be truncated
        Assert.True(capturedInput.Length <= _options.MaxInputLength,
            $"Input should be truncated to {_options.MaxInputLength} chars, but was {capturedInput.Length}");
    }

    [Fact]
    public async Task JsonInMarkdownCodeBlock_ParsedCorrectly()
    {
        // Arrange: LLM returns JSON wrapped in markdown code block
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = @"Here's my suggestion:

```json
{
    ""suggested"": true,
    ""command"": ""plan"",
    ""arguments"": [],
    ""confidence"": 0.85,
    ""reasoning"": ""User wants to plan"",
    ""formatted"": ""/plan""
}
```"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("make a plan");

        // Assert: Should extract and parse JSON from code block
        Assert.NotNull(result);
        Assert.Equal("plan", result!.CommandName);
        Assert.Equal(0.85, result.Confidence);
    }

    [Fact]
    public async Task StructuredPayload_WithMultipleCommands_RejectedAsSchemaMismatch()
    {
        // Arrange: Response attempts to provide multiple commands, which schema disallows
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = @"{
                        ""intent"": {
                            ""goal"": ""Execute one command""
                        },
                        ""command"": ""/run /verify"",
                        ""commands"": [""/run"", ""/verify""],
                        ""group"": ""workflow"",
                        ""rationale"": ""Trying to send two actions at once."",
                        ""expectedOutcome"": ""One command should be selected for execution.""
                    }"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("execute now");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StructuredPayload_MultiArgumentParameters_MappedToArguments()
    {
        _llmProviderMock.Setup(x => x.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage
                {
                    Role = LlmMessageRole.Assistant,
                    Content = @"{
                        ""intent"": {
                            ""goal"": ""Plan the phase"",
                            ""parameters"": {
                                ""mode"": ""incremental"",
                                ""phase-id"": ""PH-0010""
                            }
                        },
                        ""command"": ""/plan"",
                        ""group"": ""workflow"",
                        ""rationale"": ""The user asked to create a targeted phase plan."",
                        ""expectedOutcome"": ""A /plan command with parameters is ready for confirmation.""
                    }"
                },
                Model = "gpt-4",
                Provider = "fake"
            });

        var suggester = CreateSuggester();

        // Act
        var result = await suggester.SuggestAsync("plan phase 10 in incremental mode");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("plan", result!.CommandName);
        Assert.Equal(new[] { "--mode", "incremental", "--phase-id", "PH-0010" }, result.Arguments);
    }

    [Fact]
    public void CommandProposal_EmptyCommandName_Invalid()
    {
        // Arrange & Act
        var proposal = new CommandProposal
        {
            CommandName = "",
            Confidence = 0.9
        };

        // Assert
        Assert.False(proposal.IsValid());
        var errors = proposal.GetValidationErrors();
        Assert.Contains(errors, e => e.Contains("CommandName"));
    }

    [Fact]
    public void CommandProposal_NegativeConfidence_Invalid()
    {
        // Arrange & Act
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Confidence = -0.1
        };

        // Assert
        Assert.False(proposal.IsValid());
        var errors = proposal.GetValidationErrors();
        Assert.Contains(errors, e => e.Contains("Confidence"));
    }

    [Fact]
    public void CommandProposal_OverOneConfidence_Invalid()
    {
        // Arrange & Act
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Confidence = 1.1
        };

        // Assert
        Assert.False(proposal.IsValid());
        var errors = proposal.GetValidationErrors();
        Assert.Contains(errors, e => e.Contains("Confidence"));
    }

    [Fact]
    public void CommandProposal_ZeroConfidence_Valid()
    {
        // Arrange & Act
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Confidence = 0.0
        };

        // Assert: 0.0 is at the boundary and should be valid
        Assert.True(proposal.IsValid());
    }

    [Fact]
    public void CommandProposal_OneConfidence_Valid()
    {
        // Arrange & Act
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Confidence = 1.0
        };

        // Assert: 1.0 is at the boundary and should be valid
        Assert.True(proposal.IsValid());
    }

    [Fact]
    public void IntentClassificationResult_Suggestion_WithInvalidProposal_HasSuggestionReturnsFalse()
    {
        // Arrange: Create a proposal that fails validation
        var proposal = new CommandProposal
        {
            CommandName = "",  // Invalid - empty
            Confidence = 2.0    // Invalid - over 1.0
        };

        var result = IntentClassificationResult.Suggestion(
            new Intent
            {
                Kind = IntentKind.WorkflowCommand,
                SideEffect = SideEffect.Write,
                Confidence = 0.9,
                Reasoning = "Invalid suggestion"
            },
            proposal);

        // Act & Assert
        Assert.False(result.HasSuggestion());
    }
}

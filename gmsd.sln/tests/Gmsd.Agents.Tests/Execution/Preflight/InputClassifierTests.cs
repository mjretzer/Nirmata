using Gmsd.Agents.Execution.Preflight;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Preflight;

public class InputClassifierTests
{
    private readonly InputClassifier _classifier;

    public InputClassifierTests()
    {
        _classifier = new InputClassifier();
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("hi")]
    [InlineData("hey")]
    [InlineData("thanks")]
    [InlineData("bye")]
    public void Classify_Greeting_ReturnsSmallTalk(string input)
    {
        var result = _classifier.Classify(input);

        Assert.Equal(IntentKind.SmallTalk, result.Intent.Kind);
        Assert.Equal(SideEffect.None, result.Intent.SideEffect);
        Assert.Equal(1.0, result.Intent.Confidence);
    }

    [Theory]
    [InlineData("/run")]
    [InlineData("/plan")]
    [InlineData("/verify")]
    [InlineData("/fix")]
    [InlineData("/pause")]
    [InlineData("/resume")]
    public void Classify_WriteCommand_ReturnsWriteIntent(string input)
    {
        var result = _classifier.Classify(input);

        Assert.Equal(IntentKind.WorkflowCommand, result.Intent.Kind);
        Assert.Equal(SideEffect.Write, result.Intent.SideEffect);
        Assert.Equal(1.0, result.Intent.Confidence);
        Assert.NotNull(result.ParsedCommand);
        Assert.True(result.ParsedCommand!.IsKnownCommand);
    }

    [Theory]
    [InlineData("/status")]
    [InlineData("/help")]
    [InlineData("/?")]
    public void Classify_ReadOnlyCommand_ReturnsReadOnlyIntent(string input)
    {
        var result = _classifier.Classify(input);

        Assert.Equal(SideEffect.ReadOnly, result.Intent.SideEffect);
        Assert.Equal(1.0, result.Intent.Confidence);
        Assert.NotNull(result.ParsedCommand);
    }

    [Fact]
    public void Classify_StatusCommand_ReturnsStatusKind()
    {
        var result = _classifier.Classify("/status");

        Assert.Equal(IntentKind.Status, result.Intent.Kind);
    }

    [Fact]
    public void Classify_HelpCommand_ReturnsHelpKind()
    {
        var result = _classifier.Classify("/help");

        Assert.Equal(IntentKind.Help, result.Intent.Kind);
    }

    [Theory]
    [InlineData("create a plan")]
    [InlineData("run the tests")]
    [InlineData("execute this task")]
    [InlineData("fix the bug")]
    [InlineData("verify the changes")]
    public void Classify_FreeformWithKeywords_ReturnsChat(string input)
    {
        var result = _classifier.Classify(input);

        // These should NOT trigger workflow anymore - they are chat
        Assert.Equal(SideEffect.None, result.Intent.SideEffect);
        Assert.Equal(IntentKind.Unknown, result.Intent.Kind);
        Assert.True(result.Intent.Confidence >= 0.9);
    }

    [Fact]
    public void Classify_EmptyInput_ReturnsUnknown()
    {
        var result = _classifier.Classify("");

        Assert.Equal(IntentKind.Unknown, result.Intent.Kind);
        Assert.Equal(SideEffect.None, result.Intent.SideEffect);
        Assert.Equal(1.0, result.Intent.Confidence);
    }

    [Fact]
    public void Classify_UnknownCommand_ReturnsUnknownWithSuggestions()
    {
        var result = _classifier.Classify("/runn");  // Typo

        Assert.Equal(IntentKind.Unknown, result.Intent.Kind);
        Assert.Equal(SideEffect.None, result.Intent.SideEffect);
        Assert.NotNull(result.ParsedCommand);
        Assert.False(result.ParsedCommand!.IsKnownCommand);
        Assert.NotNull(result.ParsedCommand.Suggestions);
    }

    [Theory]
    [InlineData("just a chat message")]
    [InlineData("can you help me understand this?")]
    [InlineData("what do you think about this approach?")]
    [InlineData("tell me more about the project")]
    public void Classify_FreeformChat_ReturnsChat(string input)
    {
        var result = _classifier.Classify(input);

        Assert.Equal(SideEffect.None, result.Intent.SideEffect);
        Assert.Equal(IntentKind.Unknown, result.Intent.Kind);
        Assert.Contains("freeform chat", result.Intent.Reasoning);
    }

    [Fact]
    public void Classify_CommandWithArgs_PreservesArgs()
    {
        var result = _classifier.Classify("/run --verbose test-suite");

        Assert.NotNull(result.ParsedCommand);
        Assert.Equal("run", result.ParsedCommand!.CommandName);
        Assert.Equal(new[] { "--verbose", "test-suite" }, result.ParsedCommand.Arguments);
    }

    [Fact]
    public void Classify_TrimsWhitespace()
    {
        var result = _classifier.Classify("  /run  ");

        Assert.NotNull(result.ParsedCommand);
        Assert.Equal("run", result.ParsedCommand!.CommandName);
    }

    [Fact]
    public void ClassifyLegacy_ReturnsJustIntent()
    {
        var result = _classifier.ClassifyLegacy("/run");

        Assert.Equal(IntentKind.WorkflowCommand, result.Kind);
        Assert.Equal(SideEffect.Write, result.SideEffect);
    }

    [Fact]
    public void ClassifyResult_ContainsClassificationMetadata()
    {
        var result = _classifier.Classify("/run");

        Assert.NotNull(result);
        Assert.NotNull(result.Intent);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal("prefix_match", result.ClassificationMethod);
        Assert.False(result.RequiresConfirmation);
        Assert.True(result.ClassifiedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Classify_StructuredCommandInput_UsesStructuredClassificationMethod()
    {
        const string input = """
                             {
                               "intent": {
                                 "goal": "Plan a phase",
                                 "parameters": {
                                   "phase-id": "PH-1001"
                                 }
                               },
                               "command": "/plan",
                               "group": "workflow",
                               "rationale": "The user requested phase planning in explicit terms.",
                               "expectedOutcome": "The system prepares a plan command for confirmation."
                             }
                             """;

        var result = _classifier.Classify(input);

        Assert.NotNull(result.ParsedCommand);
        Assert.Equal("plan", result.ParsedCommand!.CommandName);
        Assert.Equal("structured_match", result.ClassificationMethod);
    }

    [Fact]
    public void Classify_InvalidStructuredCommandInput_UsesStructuredRejectedMethod()
    {
        const string input = """
                             {
                               "intent": {
                                 "goal": "Plan a phase"
                               },
                               "group": "workflow",
                               "rationale": "The payload is missing command.",
                               "expectedOutcome": "Classifier should reject the structured payload."
                             }
                             """;

        var result = _classifier.Classify(input);

        Assert.Null(result.ParsedCommand);
        Assert.Equal("structured_rejected", result.ClassificationMethod);
        Assert.Equal(IntentKind.Unknown, result.Intent.Kind);
    }
}

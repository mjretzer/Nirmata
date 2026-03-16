using nirmata.Agents.Execution.Preflight;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Preflight;

public class CommandParserTests
{
    private readonly CommandParser _parser;

    public CommandParserTests()
    {
        var registry = new CommandRegistry();
        _parser = new CommandParser(registry);
    }

    [Theory]
    [InlineData("/run", "run", SideEffect.Write)]
    [InlineData("/plan", "plan", SideEffect.Write)]
    [InlineData("/verify", "verify", SideEffect.Write)]
    [InlineData("/fix", "fix", SideEffect.Write)]
    [InlineData("/pause", "pause", SideEffect.Write)]
    [InlineData("/resume", "resume", SideEffect.Write)]
    [InlineData("/status", "status", SideEffect.ReadOnly)]
    [InlineData("/help", "help", SideEffect.ReadOnly)]
    public void Parse_KnownCommand_ReturnsCorrectSideEffect(string input, string expectedCommand, SideEffect expectedSideEffect)
    {
        var result = _parser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedCommand, result.CommandName);
        Assert.Equal(expectedSideEffect, result.SideEffect);
        Assert.True(result.IsKnownCommand);
        Assert.Equal(1.0, result.Confidence);
    }

    [Theory]
    [InlineData("/run with args", "run", new[] { "with", "args" })]
    [InlineData("/plan my feature", "plan", new[] { "my", "feature" })]
    [InlineData("/status --verbose", "status", new[] { "--verbose" })]
    public void Parse_CommandWithArguments_ExtractsArguments(string input, string expectedCommand, string[] expectedArgs)
    {
        var result = _parser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedCommand, result.CommandName);
        Assert.Equal(expectedArgs, result.Arguments);
    }

    [Fact]
    public void Parse_UnknownCommand_ReturnsUnknownWithSuggestions()
    {
        var result = _parser.Parse("/runn");  // Typo for "run"

        Assert.NotNull(result);
        Assert.Equal("runn", result.CommandName);
        Assert.False(result.IsKnownCommand);
        Assert.NotNull(result.Suggestions);
        Assert.Contains("run", result.Suggestions);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("create a plan")]
    [InlineData("run the tests")]
    [InlineData("fix the bug")]
    [InlineData("")]
    public void Parse_NonCommandInput_ReturnsNull(string input)
    {
        var result = _parser.Parse(input);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
#pragma warning disable CS8625
        var result = _parser.Parse(null);
#pragma warning restore CS8625

        Assert.Null(result);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("create a plan")]
    [InlineData("run the tests")]
    [InlineData("fix the bug")]
    [InlineData("just chatting")]
    public void IsChatInput_FreeformText_ReturnsTrue(string input)
    {
        var result = _parser.IsChatInput(input);

        Assert.True(result);
    }

    [Theory]
    [InlineData("/run")]
    [InlineData("/plan")]
    [InlineData("/status")]
    [InlineData("/unknown")]
    public void IsChatInput_CommandPrefix_ReturnsFalse(string input)
    {
        var result = _parser.IsChatInput(input);

        Assert.False(result);
    }

    [Theory]
    [InlineData("  /run  ", "run")]
    [InlineData("\t/plan\t", "plan")]
    public void Parse_InputWithWhitespace_TrimsCorrectly(string input, string expectedCommand)
    {
        var result = _parser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedCommand, result.CommandName);
    }

    [Fact]
    public void Parse_CommandWithAlias_RecognizesAlias()
    {
        var result = _parser.Parse("/?");

        Assert.NotNull(result);
        Assert.Equal("?", result.CommandName);
        Assert.True(result.IsKnownCommand);
        Assert.Equal(SideEffect.ReadOnly, result.SideEffect);
    }

    [Fact]
    public void ParseDetailed_StructuredIntent_MapsToCommand()
    {
        const string input = """
                             {
                               "intent": {
                                 "goal": "Create plan",
                                 "parameters": {
                                   "phase-id": "PH-0001",
                                   "mode": "incremental"
                                 }
                               },
                               "command": "/plan",
                               "group": "workflow",
                               "rationale": "User asked to plan the next phase in detail.",
                               "expectedOutcome": "A structured plan command is produced for confirmation."
                             }
                             """;

        var result = _parser.ParseDetailed(input);

        Assert.NotNull(result.ParsedCommand);
        Assert.True(result.IsStructuredInput);
        Assert.Equal("structured", result.ParseMode);
        Assert.Equal("structured", result.ParsedCommand!.ParseMode);
        Assert.Equal("plan", result.ParsedCommand.CommandName);
        Assert.Equal(new[] { "--mode", "incremental", "--phase-id", "PH-0001" }, result.ParsedCommand.Arguments);
    }

    [Fact]
    public void ParseDetailed_StructuredIntentMissingCommand_RejectedWithActionableMessage()
    {
        const string input = """
                             {
                               "intent": {
                                 "goal": "Run tests",
                                 "parameters": {
                                   "suite": "unit"
                                 }
                               },
                               "group": "workflow",
                               "rationale": "Trying to run validation.",
                               "expectedOutcome": "Tests should run."
                             }
                             """;

        var result = _parser.ParseDetailed(input);

        Assert.Null(result.ParsedCommand);
        Assert.True(result.IsStructuredInput);
        Assert.Equal("structured", result.ParseMode);
        Assert.Contains("missing required string field 'command'", result.ValidationMessage);
    }

    [Fact]
    public void ParseDetailed_StructuredIntentWithExtraCommands_RejectedWithActionableMessage()
    {
        const string input = """
                             {
                               "intent": {
                                 "goal": "Run one command"
                               },
                               "command": "/status",
                               "commands": ["/status", "/run"],
                               "group": "workflow",
                               "rationale": "This includes too many commands.",
                               "expectedOutcome": "Parser should reject ambiguous payload."
                             }
                             """;

        var result = _parser.ParseDetailed(input);

        Assert.Null(result.ParsedCommand);
        Assert.True(result.IsStructuredInput);
        Assert.Equal("structured", result.ParseMode);
        Assert.Contains("single 'command' value", result.ValidationMessage);
    }
}

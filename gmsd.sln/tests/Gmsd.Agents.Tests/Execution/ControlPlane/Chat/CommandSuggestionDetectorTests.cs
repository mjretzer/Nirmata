using Gmsd.Agents.Execution.ControlPlane.Chat;
using Gmsd.Agents.Execution.ControlPlane.Commands;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Chat;

public class CommandSuggestionDetectorTests
{
    private readonly CommandSuggestionDetector _detector;
    private readonly ICommandParser _commandParser;
    private readonly ICommandRegistry _commandRegistry;

    public CommandSuggestionDetectorTests()
    {
        _commandParser = new CommandParser();
        _commandRegistry = new CommandRegistry();
        _detector = new CommandSuggestionDetector(_commandParser, _commandRegistry);
    }

    [Fact]
    public void DetectSuggestion_WithHelpKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("Can you help me?");

        Assert.NotNull(result);
        Assert.Equal("help", result.CommandName);
        Assert.True(result.Confidence >= 0.9);
    }

    [Fact]
    public void DetectSuggestion_WithStatusKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("What's the current status?");

        Assert.NotNull(result);
        Assert.Equal("status", result.CommandName);
        Assert.True(result.Confidence >= 0.8);
    }

    [Fact]
    public void DetectSuggestion_WithRunKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("Can you run the build workflow?");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.True(result.Confidence >= 0.7);
    }

    [Fact]
    public void DetectSuggestion_WithRunKeywordAndWorkflow_ExtractsWorkflowName()
    {
        var result = _detector.DetectSuggestion("Please run my-workflow");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Contains("workflow", result.Arguments.Keys);
        Assert.Equal("my-workflow", result.Arguments["workflow"]);
        Assert.True(result.Confidence >= 0.8);
    }

    [Fact]
    public void DetectSuggestion_WithPlanKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("I need to plan a new feature");

        Assert.NotNull(result);
        Assert.Equal("plan", result.CommandName);
        Assert.True(result.Confidence >= 0.6);
    }

    [Fact]
    public void DetectSuggestion_WithPlanKeywordAndTask_ExtractsTaskDescription()
    {
        var result = _detector.DetectSuggestion("Can you plan implementing authentication?");

        Assert.NotNull(result);
        Assert.Equal("plan", result.CommandName);
        Assert.Contains("task", result.Arguments.Keys);
        Assert.True(result.Confidence >= 0.7);
    }

    [Fact]
    public void DetectSuggestion_WithVerifyKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("Please verify the build");

        Assert.NotNull(result);
        Assert.Equal("verify", result.CommandName);
        Assert.True(result.Confidence >= 0.7);
    }

    [Fact]
    public void DetectSuggestion_WithVerifyKeywordAndTarget_ExtractsTarget()
    {
        var result = _detector.DetectSuggestion("Can you verify the database connection?");

        Assert.NotNull(result);
        Assert.Equal("verify", result.CommandName);
        Assert.Contains("target", result.Arguments.Keys);
        Assert.True(result.Confidence >= 0.8);
    }

    [Fact]
    public void DetectSuggestion_WithFixKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("I need to fix this issue");

        Assert.NotNull(result);
        Assert.Equal("fix", result.CommandName);
        Assert.True(result.Confidence >= 0.6);
    }

    [Fact]
    public void DetectSuggestion_WithFixKeywordAndIssue_ExtractsIssue()
    {
        var result = _detector.DetectSuggestion("Can you fix the failing tests?");

        Assert.NotNull(result);
        Assert.Equal("fix", result.CommandName);
        Assert.Contains("issue", result.Arguments.Keys);
        Assert.True(result.Confidence >= 0.7);
    }

    [Fact]
    public void DetectSuggestion_WithoutCommandKeywords_ReturnsNull()
    {
        var result = _detector.DetectSuggestion("This is just a regular chat message");

        Assert.Null(result);
    }

    [Fact]
    public void DetectSuggestion_WithEmptyInput_ReturnsNull()
    {
        var result = _detector.DetectSuggestion("");

        Assert.Null(result);
    }

    [Fact]
    public void DetectSuggestion_WithWhitespaceOnly_ReturnsNull()
    {
        var result = _detector.DetectSuggestion("   ");

        Assert.Null(result);
    }

    [Fact]
    public void DetectSuggestion_WithCaseInsensitiveKeyword_ReturnsSuggestion()
    {
        var result1 = _detector.DetectSuggestion("HELP me");
        var result2 = _detector.DetectSuggestion("Help me");
        var result3 = _detector.DetectSuggestion("help me");

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.Equal("help", result1.CommandName);
        Assert.Equal("help", result2.CommandName);
        Assert.Equal("help", result3.CommandName);
    }

    [Fact]
    public void DetectSuggestion_WithMultipleKeywords_DetectsFirst()
    {
        var result = _detector.DetectSuggestion("Can you help me run this workflow?");

        Assert.NotNull(result);
        // Should detect "help" first since it appears first in the input
        Assert.Equal("help", result.CommandName);
    }

    [Fact]
    public void DetectSuggestion_WithQuotedArguments_PreservesQuotes()
    {
        var result = _detector.DetectSuggestion("Run \"my-workflow-name\"");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Contains("workflow", result.Arguments.Keys);
    }

    [Fact]
    public void DetectSuggestion_IncludesReasoning()
    {
        var result = _detector.DetectSuggestion("Can you help me?");

        Assert.NotNull(result);
        Assert.NotNull(result.Reasoning);
        Assert.NotEmpty(result.Reasoning);
    }

    [Fact]
    public void DetectSuggestion_WithExecuteKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("Execute the deployment workflow");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
    }

    [Fact]
    public void DetectSuggestion_WithCheckKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("Check the system status");

        Assert.NotNull(result);
        Assert.Equal("verify", result.CommandName);
    }

    [Fact]
    public void DetectSuggestion_WithRepairKeyword_ReturnsSuggestion()
    {
        var result = _detector.DetectSuggestion("Repair the broken build");

        Assert.NotNull(result);
        Assert.Equal("fix", result.CommandName);
    }
}

using Gmsd.Agents.Execution.ControlPlane.Commands;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Commands;

public class CommandParserTests
{
    private readonly CommandParser _parser = new();

    [Fact]
    public void TryParseCommand_WithValidCommand_ReturnsCommand()
    {
        var result = _parser.TryParseCommand("/help");

        Assert.NotNull(result);
        Assert.Equal("help", result.CommandName);
        Assert.Empty(result.Arguments);
    }

    [Fact]
    public void TryParseCommand_WithCommandAndPositionalArgs_ParsesCorrectly()
    {
        var result = _parser.TryParseCommand("/run workflow-name");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Single(result.Arguments);
        Assert.Equal("workflow-name", result.Arguments["arg0"]);
    }

    [Fact]
    public void TryParseCommand_WithKeyValueArgs_ParsesCorrectly()
    {
        var result = _parser.TryParseCommand("/plan task=build-feature");

        Assert.NotNull(result);
        Assert.Equal("plan", result.CommandName);
        Assert.Single(result.Arguments);
        Assert.Equal("build-feature", result.Arguments["task"]);
    }

    [Fact]
    public void TryParseCommand_WithMixedArgs_ParsesCorrectly()
    {
        var result = _parser.TryParseCommand("/run my-workflow task=build-feature priority=high");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Equal(3, result.Arguments.Count);
        Assert.Equal("my-workflow", result.Arguments["arg0"]);
        Assert.Equal("build-feature", result.Arguments["task"]);
        Assert.Equal("high", result.Arguments["priority"]);
    }

    [Fact]
    public void TryParseCommand_WithQuotedArgs_PreservesQuotedContent()
    {
        var result = _parser.TryParseCommand("/run \"my workflow\" task=\"build feature\"");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Equal("my workflow", result.Arguments["arg0"]);
        Assert.Equal("build feature", result.Arguments["task"]);
    }

    [Fact]
    public void TryParseCommand_WithEscapedQuotes_HandlesCorrectly()
    {
        var result = _parser.TryParseCommand("/run arg=value\\\"with\\\"quotes");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Equal("value\"with\"quotes", result.Arguments["arg"]);
    }

    [Fact]
    public void TryParseCommand_WithoutSlashPrefix_ReturnsNull()
    {
        var result = _parser.TryParseCommand("help");

        Assert.Null(result);
    }

    [Fact]
    public void TryParseCommand_WithOnlySlash_ReturnsNull()
    {
        var result = _parser.TryParseCommand("/");

        Assert.Null(result);
    }

    [Fact]
    public void TryParseCommand_WithEmptyString_ReturnsNull()
    {
        var result = _parser.TryParseCommand("");

        Assert.Null(result);
    }

    [Fact]
    public void TryParseCommand_WithWhitespaceOnly_ReturnsNull()
    {
        var result = _parser.TryParseCommand("   ");

        Assert.Null(result);
    }

    [Fact]
    public void TryParseCommand_WithLeadingWhitespace_TrimsAndParses()
    {
        var result = _parser.TryParseCommand("   /help");

        Assert.NotNull(result);
        Assert.Equal("help", result.CommandName);
    }

    [Fact]
    public void TryParseCommand_WithMultipleSpaces_ParsesCorrectly()
    {
        var result = _parser.TryParseCommand("/run   workflow-name   arg=value");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Equal("workflow-name", result.Arguments["arg0"]);
        Assert.Equal("value", result.Arguments["arg"]);
    }

    [Fact]
    public void TryParseCommand_PreservesRawInput()
    {
        var input = "/help command=test";
        var result = _parser.TryParseCommand(input);

        Assert.NotNull(result);
        Assert.Equal(input, result.RawInput);
    }

    [Fact]
    public void IsCommand_WithSlashPrefix_ReturnsTrue()
    {
        Assert.True(_parser.IsCommand("/help"));
        Assert.True(_parser.IsCommand("/run workflow"));
    }

    [Fact]
    public void IsCommand_WithoutSlashPrefix_ReturnsFalse()
    {
        Assert.False(_parser.IsCommand("help"));
        Assert.False(_parser.IsCommand("this is a chat message"));
    }

    [Fact]
    public void IsCommand_WithEmptyString_ReturnsFalse()
    {
        Assert.False(_parser.IsCommand(""));
    }

    [Fact]
    public void IsCommand_WithWhitespaceOnly_ReturnsFalse()
    {
        Assert.False(_parser.IsCommand("   "));
    }

    [Fact]
    public void IsCommand_WithLeadingWhitespaceAndSlash_ReturnsTrue()
    {
        Assert.True(_parser.IsCommand("   /help"));
    }

    [Fact]
    public void TryParseCommand_CommandNameIsCaseInsensitive()
    {
        var result1 = _parser.TryParseCommand("/HELP");
        var result2 = _parser.TryParseCommand("/Help");
        var result3 = _parser.TryParseCommand("/help");

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.Equal("help", result1.CommandName);
        Assert.Equal("help", result2.CommandName);
        Assert.Equal("help", result3.CommandName);
    }

    [Fact]
    public void TryParseCommand_WithEmptyQuotes_PreservesEmptyString()
    {
        var result = _parser.TryParseCommand("/run \"\"");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Equal("", result.Arguments["arg0"]);
    }

    [Fact]
    public void TryParseCommand_WithSpecialCharactersInArgs_ParsesCorrectly()
    {
        var result = _parser.TryParseCommand("/run path=/home/user/file.txt");

        Assert.NotNull(result);
        Assert.Equal("run", result.CommandName);
        Assert.Equal("/home/user/file.txt", result.Arguments["path"]);
    }
}

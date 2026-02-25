using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests;

public class MessageNormalizationTests
{
    [Theory]
    [InlineData(LlmMessageRole.System, "system")]
    [InlineData(LlmMessageRole.User, "user")]
    [InlineData(LlmMessageRole.Assistant, "assistant")]
    [InlineData(LlmMessageRole.Tool, "tool")]
    public void LlmMessageRole_ConvertsToCorrectString(LlmMessageRole role, string expected)
    {
        // Act
        var result = role.ToString().ToLowerInvariant();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void LlmMessage_System_CreatesSystemMessage()
    {
        // Arrange & Act
        var message = LlmMessage.System("You are a helpful assistant");

        // Assert
        message.Role.Should().Be(LlmMessageRole.System);
        message.Content.Should().Be("You are a helpful assistant");
    }

    [Fact]
    public void LlmMessage_User_CreatesUserMessage()
    {
        // Arrange & Act
        var message = LlmMessage.User("Hello, world!");

        // Assert
        message.Role.Should().Be(LlmMessageRole.User);
        message.Content.Should().Be("Hello, world!");
    }

    [Fact]
    public void LlmMessage_Assistant_CreatesAssistantMessage()
    {
        // Arrange & Act
        var message = LlmMessage.Assistant("How can I help?");

        // Assert
        message.Role.Should().Be(LlmMessageRole.Assistant);
        message.Content.Should().Be("How can I help?");
    }

    [Fact]
    public void LlmMessage_Assistant_WithToolCalls_CreatesAssistantMessageWithTools()
    {
        // Arrange
        var toolCalls = new[]
        {
            new LlmToolCall { Id = "call_1", Name = "get_weather", ArgumentsJson = "{\"city\":\"Paris\"}" }
        };

        // Act
        var message = LlmMessage.Assistant(toolCalls: toolCalls);

        // Assert
        message.Role.Should().Be(LlmMessageRole.Assistant);
        message.ToolCalls.Should().NotBeNull();
        message.ToolCalls.Should().HaveCount(1);
    }

    [Fact]
    public void LlmMessage_Tool_CreatesToolResultMessage()
    {
        // Arrange & Act
        var message = LlmMessage.Tool("call_1", "get_weather", "{\"temperature\":22}");

        // Assert
        message.Role.Should().Be(LlmMessageRole.Tool);
        message.ToolCallId.Should().Be("call_1");
        message.ToolName.Should().Be("get_weather");
        message.Content.Should().Be("{\"temperature\":22}");
    }

    [Fact]
    public void LlmCompletionRequest_RequiresMessages()
    {
        // Arrange & Act
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        // Assert
        request.Messages.Should().HaveCount(1);
        request.Messages[0].Content.Should().Be("Hello");
    }

    [Fact]
    public void LlmCompletionResponse_RequiresMessageAndModel()
    {
        // Arrange & Act
        var response = new LlmCompletionResponse
        {
            Message = LlmMessage.Assistant("Hi there!"),
            Model = "gpt-4",
            Provider = "openai"
        };

        // Assert
        response.Message.Content.Should().Be("Hi there!");
        response.Model.Should().Be("gpt-4");
        response.Provider.Should().Be("openai");
    }

    [Fact]
    public void LlmTokenUsage_CalculatesTotalTokens()
    {
        // Arrange & Act
        var usage = new LlmTokenUsage
        {
            PromptTokens = 10,
            CompletionTokens = 20
        };

        // Assert
        usage.TotalTokens.Should().Be(30);
    }
}

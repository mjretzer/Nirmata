using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using System.Text.Json;
using Xunit;

namespace nirmata.Agents.Tests;

public class ToolCallSerializationTests
{
    [Fact]
    public void LlmToolCall_SerializesToValidJson()
    {
        // Arrange
        var toolCall = new LlmToolCall
        {
            Id = "call_abc123",
            Name = "get_weather",
            ArgumentsJson = "{\"city\":\"Paris\",\"units\":\"celsius\"}"
        };

        // Act
        var json = JsonSerializer.Serialize(toolCall);

        // Assert
        json.Should().Contain("\"Id\":");
        json.Should().Contain("\"Name\":");
        json.Should().Contain("\"ArgumentsJson\":");
    }

    [Fact]
    public void LlmToolCall_DeserializesFromJson()
    {
        // Arrange
        var json = """{"Id":"call_abc123","Name":"get_weather","ArgumentsJson":"{\"city\":\"Paris\"}"}""";

        // Act
        var toolCall = JsonSerializer.Deserialize<LlmToolCall>(json);

        // Assert
        toolCall.Should().NotBeNull();
        toolCall!.Id.Should().Be("call_abc123");
        toolCall.Name.Should().Be("get_weather");
        toolCall.ArgumentsJson.Should().Be("{\"city\":\"Paris\"}");
    }

    [Fact]
    public void LlmToolResult_SerializesCorrectly()
    {
        // Arrange
        var result = new LlmToolResult
        {
            ToolCallId = "call_abc123",
            ToolName = "get_weather",
            Content = "{\"temperature\":22,\"conditions\":\"sunny\"}",
            IsError = false
        };

        // Act
        var json = JsonSerializer.Serialize(result);

        // Assert
        json.Should().Contain("\"ToolCallId\":");
        json.Should().Contain("\"IsError\":false");
    }

    [Fact]
    public void LlmToolResult_WithError_SerializesErrorFlag()
    {
        // Arrange
        var result = new LlmToolResult
        {
            ToolCallId = "call_abc123",
            ToolName = "get_weather",
            Content = "Error: API timeout",
            IsError = true
        };

        // Act
        var json = JsonSerializer.Serialize(result);

        // Assert
        json.Should().Contain("\"IsError\":true");
    }

    [Fact]
    public void LlmToolDefinition_SerializesWithSchema()
    {
        // Arrange
        var schema = new
        {
            type = "object",
            properties = new
            {
                city = new { type = "string" },
                units = new { type = "string", @enum = new[] { "celsius", "fahrenheit" } }
            },
            required = new[] { "city" }
        };

        var definition = new LlmToolDefinition
        {
            Name = "get_weather",
            Description = "Get current weather for a city",
            ParametersSchema = schema
        };

        // Act
        var json = JsonSerializer.Serialize(definition);

        // Assert
        json.Should().Contain("\"Name\":\"get_weather\"");
        json.Should().Contain("\"ParametersSchema\"");
    }

    [Fact]
    public void LlmProviderException_HasCorrectProperties()
    {
        // Arrange & Act
        var exception = new LlmProviderException(
            providerName: "openai",
            message: "Rate limit exceeded",
            errorCode: "rate_limit_exceeded",
            isRetryable: true);

        // Assert
        exception.ProviderName.Should().Be("openai");
        exception.ErrorCode.Should().Be("rate_limit_exceeded");
        exception.IsRetryable.Should().BeTrue();
        exception.Message.Should().Be("Rate limit exceeded");
    }

    [Fact]
    public void LlmCompletionRequest_WithTools_SerializesCorrectly()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("What's the weather?") },
            Tools = new[]
            {
                new LlmToolDefinition
                {
                    Name = "get_weather",
                    Description = "Get weather",
                    ParametersSchema = new { type = "object" }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain("\"Tools\"");
    }
}

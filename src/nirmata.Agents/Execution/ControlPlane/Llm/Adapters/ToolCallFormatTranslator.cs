#pragma warning disable CS0618 // Intentionally using obsolete ILlmProvider contracts

using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using System.Text.Json;

namespace nirmata.Agents.Execution.ControlPlane.Llm.Adapters;

/// <summary>
/// Utility methods for translating between normalized tool call formats and provider-specific formats.
/// </summary>
public static class ToolCallFormatTranslator
{
    /// <summary>
    /// Normalizes a tool call from OpenAI format.
    /// </summary>
    public static LlmToolCall FromOpenAI(string id, string name, string argumentsJson) =>
        new()
        {
            Id = id,
            Name = name,
            ArgumentsJson = argumentsJson
        };

    /// <summary>
    /// Normalizes a tool call from Anthropic format.
    /// </summary>
    public static LlmToolCall FromAnthropic(string id, string name, object input) =>
        new()
        {
            Id = id,
            Name = name,
            ArgumentsJson = JsonSerializer.Serialize(input)
        };

    /// <summary>
    /// Normalizes a tool call from Ollama format.
    /// </summary>
    public static LlmToolCall FromOllama(string name, object arguments) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            ArgumentsJson = JsonSerializer.Serialize(arguments)
        };

    /// <summary>
    /// Converts a normalized tool call to OpenAI format.
    /// </summary>
    public static (string Id, string Name, string ArgumentsJson) ToOpenAI(LlmToolCall toolCall) =>
        (toolCall.Id, toolCall.Name, toolCall.ArgumentsJson);

    /// <summary>
    /// Converts a normalized tool result to JSON string for provider consumption.
    /// </summary>
    public static string ToToolResultJson(LlmToolResult result) =>
        JsonSerializer.Serialize(new
        {
            tool_call_id = result.ToolCallId,
            role = "tool",
            content = result.Content
        });
}

#pragma warning restore CS0618


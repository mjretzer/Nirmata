using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using System.Text.Json;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Adapters;

/// <summary>
/// Utility methods for translating between normalized LLM message formats and provider-specific formats.
/// </summary>
public static class MessageFormatTranslator
{
    /// <summary>
    /// Converts an LlmMessageRole to a standard string representation.
    /// </summary>
    public static string ToRoleString(LlmMessageRole role) => role switch
    {
        LlmMessageRole.System => "system",
        LlmMessageRole.User => "user",
        LlmMessageRole.Assistant => "assistant",
        LlmMessageRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown message role")
    };

    /// <summary>
    /// Parses a role string to LlmMessageRole.
    /// </summary>
    public static LlmMessageRole FromRoleString(string role) =>
        role?.ToLowerInvariant() switch
        {
            "system" => LlmMessageRole.System,
            "user" => LlmMessageRole.User,
            "assistant" => LlmMessageRole.Assistant,
            "tool" => LlmMessageRole.Tool,
            _ => throw new ArgumentException($"Unknown role: {role}", nameof(role))
        };

    /// <summary>
    /// Normalizes message content from various provider formats.
    /// </summary>
    public static string? NormalizeContent(string? content, object? structuredContent = null)
    {
        if (!string.IsNullOrEmpty(content))
            return content;

        if (structuredContent != null)
            return JsonSerializer.Serialize(structuredContent);

        return null;
    }
}

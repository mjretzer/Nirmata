using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace nirmata.Agents.Execution.ControlPlane.Llm.Anthropic;

/// <summary>
/// Prompt execution settings for Anthropic Claude models.
/// </summary>
public sealed class ClaudePromptExecutionSettings : PromptExecutionSettings
{
    /// <summary>
    /// The maximum number of tokens to generate before stopping.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    [DefaultValue(2048)]
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Amount of randomness injected into the response.
    /// Values range from 0.0 to 1.0.
    /// </summary>
    [JsonPropertyName("temperature")]
    [DefaultValue(1.0)]
    public double Temperature { get; set; } = 1.0;

    /// <summary>
    /// Only sample from the top K options for each subsequent token.
    /// </summary>
    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    /// <summary>
    /// Use nucleus sampling.
    /// In nucleus sampling, we compute the cumulative distribution over all the options for each subsequent token in decreasing probability order and cut it off once it reaches a particular probability specified by top_p.
    /// Values range from 0.0 to 1.0.
    /// </summary>
    [JsonPropertyName("top_p")]
    [DefaultValue(1.0)]
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// An object containing metadata about the tools used in the request.
    /// </summary>
    [JsonPropertyName("tools")]
    public IList<ClaudeToolDefinition>? Tools { get; set; }

    /// <summary>
    /// How the model should use the provided tools.
    /// </summary>
    [JsonPropertyName("tool_choice")]
    public ClaudeToolChoice? ToolChoice { get; set; }

    /// <summary>
    /// System prompt to provide context and instructions to Claude.
    /// </summary>
    [JsonPropertyName("system")]
    public string? System { get; set; }

    /// <summary>
    /// A version string for the Anthropic API.
    /// </summary>
    [JsonPropertyName("anthropic_version")]
    [DefaultValue("2023-06-01")]
    public string AnthropicVersion { get; set; } = "2023-06-01";

    /// <summary>
    /// Creates a copy of this instance with the same settings.
    /// </summary>
    /// <returns>A clone of this instance.</returns>
    public override PromptExecutionSettings Clone()
    {
        return new ClaudePromptExecutionSettings
        {
            ModelId = this.ModelId,
            MaxTokens = this.MaxTokens,
            Temperature = this.Temperature,
            TopK = this.TopK,
            TopP = this.TopP,
            Tools = this.Tools?.ToList(),
            ToolChoice = this.ToolChoice,
            System = this.System,
            AnthropicVersion = this.AnthropicVersion
        };
    }
}

/// <summary>
/// Represents a tool definition for Claude.
/// </summary>
public sealed class ClaudeToolDefinition
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// A description of what the tool does.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// The JSON schema for the tool's input.
    /// </summary>
    [JsonPropertyName("input_schema")]
    public required ClaudeInputSchema InputSchema { get; set; }
}

/// <summary>
/// Represents the input schema for a Claude tool.
/// </summary>
public sealed class ClaudeInputSchema
{
    /// <summary>
    /// The type of the input (typically "object").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Properties defined in the schema.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, ClaudeSchemaProperty>? Properties { get; set; }

    /// <summary>
    /// Required properties.
    /// </summary>
    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

/// <summary>
/// Represents a property in the Claude input schema.
/// </summary>
public sealed class ClaudeSchemaProperty
{
    /// <summary>
    /// The type of the property.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Description of the property.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Enum values if applicable.
    /// </summary>
    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }
}

/// <summary>
/// Represents tool choice options for Claude.
/// </summary>
public sealed class ClaudeToolChoice
{
    /// <summary>
    /// The type of tool choice (e.g., "auto", "any", "tool").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// The name of the tool to use when type is "tool".
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

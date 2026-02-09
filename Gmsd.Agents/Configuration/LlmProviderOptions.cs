namespace Gmsd.Agents.Configuration;

/// <summary>
/// Configuration options for OpenAI LLM provider.
/// </summary>
public sealed class OpenAiLlmOptions
{
    /// <summary>
    /// OpenAI API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Default model to use (e.g., "gpt-4", "gpt-3.5-turbo").
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-4o";

    /// <summary>
    /// Optional base URL override for proxy or enterprise deployments.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Organization ID for enterprise accounts.
    /// </summary>
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Configuration options for Anthropic LLM provider.
/// </summary>
public sealed class AnthropicLlmOptions
{
    /// <summary>
    /// Anthropic API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Default model to use (e.g., "claude-3-opus-20240229").
    /// </summary>
    public string DefaultModel { get; set; } = "claude-3-sonnet-20240229";

    /// <summary>
    /// Optional base URL override.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Anthropic API version.
    /// </summary>
    public string ApiVersion { get; set; } = "2023-06-01";
}

/// <summary>
/// Configuration options for Azure OpenAI LLM provider.
/// </summary>
public sealed class AzureOpenAiLlmOptions
{
    /// <summary>
    /// Azure OpenAI endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Default deployment name.
    /// </summary>
    public string DefaultDeployment { get; set; } = string.Empty;

    /// <summary>
    /// API version for Azure OpenAI.
    /// </summary>
    public string ApiVersion { get; set; } = "2024-02-01";
}

/// <summary>
/// Configuration options for Ollama LLM provider.
/// </summary>
public sealed class OllamaLlmOptions
{
    /// <summary>
    /// Ollama server base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Default model to use.
    /// </summary>
    public string DefaultModel { get; set; } = "llama3";
}

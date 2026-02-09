using System.ComponentModel.DataAnnotations;

namespace Gmsd.Agents.Configuration;

/// <summary>
/// Configuration options for Microsoft Semantic Kernel integration.
/// </summary>
public sealed class SemanticKernelOptions
{
    /// <summary>
    /// Configuration section name for Semantic Kernel settings.
    /// </summary>
    public const string SectionName = "GmsdAgents:SemanticKernel";

    /// <summary>
    /// The LLM provider to use with Semantic Kernel.
    /// Valid values: OpenAi, AzureOpenAi, Ollama, Anthropic
    /// </summary>
    [Required(ErrorMessage = "Provider is required")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI-specific options (used when Provider = "OpenAi").
    /// </summary>
    public OpenAiSemanticKernelOptions? OpenAi { get; set; }

    /// <summary>
    /// Azure OpenAI-specific options (used when Provider = "AzureOpenAi").
    /// </summary>
    public AzureOpenAiSemanticKernelOptions? AzureOpenAi { get; set; }

    /// <summary>
    /// Ollama-specific options (used when Provider = "Ollama").
    /// </summary>
    public OllamaSemanticKernelOptions? Ollama { get; set; }

    /// <summary>
    /// Anthropic-specific options (used when Provider = "Anthropic").
    /// </summary>
    public AnthropicSemanticKernelOptions? Anthropic { get; set; }
}

/// <summary>
/// Configuration options for OpenAI connector in Semantic Kernel.
/// </summary>
public sealed class OpenAiSemanticKernelOptions
{
    /// <summary>
    /// OpenAI API key.
    /// </summary>
    [Required(ErrorMessage = "OpenAI API key is required")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model ID to use (e.g., "gpt-4", "gpt-4o", "gpt-3.5-turbo").
    /// </summary>
    [Required(ErrorMessage = "OpenAI model ID is required")]
    public string ModelId { get; set; } = "gpt-4o";

    /// <summary>
    /// Optional organization ID for enterprise accounts.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Optional base URL override for proxy or enterprise deployments.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Temperature for sampling (0.0 to 2.0).
    /// Default: 1.0
    /// </summary>
    [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0.0 and 2.0")]
    public double Temperature { get; set; } = 1.0;

    /// <summary>
    /// Maximum number of tokens to generate.
    /// Default: 2048
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "MaxTokens must be at least 1")]
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Nucleus sampling parameter (0.0 to 1.0).
    /// Default: 1.0
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "TopP must be between 0.0 and 1.0")]
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// Penalty for repeating tokens (-2.0 to 2.0).
    /// Default: 0.0
    /// </summary>
    [Range(-2.0, 2.0, ErrorMessage = "FrequencyPenalty must be between -2.0 and 2.0")]
    public double FrequencyPenalty { get; set; } = 0.0;

    /// <summary>
    /// Penalty for new tokens based on presence in text so far (-2.0 to 2.0).
    /// Default: 0.0
    /// </summary>
    [Range(-2.0, 2.0, ErrorMessage = "PresencePenalty must be between -2.0 and 2.0")]
    public double PresencePenalty { get; set; } = 0.0;

    /// <summary>
    /// Seed for deterministic sampling.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// Whether to enable parallel tool calling.
    /// Default: true
    /// </summary>
    public bool EnableParallelToolCalls { get; set; } = true;
}

/// <summary>
/// Configuration options for Azure OpenAI connector in Semantic Kernel.
/// </summary>
public sealed class AzureOpenAiSemanticKernelOptions
{
    /// <summary>
    /// Azure OpenAI endpoint URL.
    /// </summary>
    [Required(ErrorMessage = "Azure OpenAI endpoint is required")]
    [Url(ErrorMessage = "Endpoint must be a valid URL")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API key.
    /// </summary>
    [Required(ErrorMessage = "Azure OpenAI API key is required")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name for the Azure OpenAI model.
    /// </summary>
    [Required(ErrorMessage = "Azure OpenAI deployment name is required")]
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// API version for Azure OpenAI.
    /// Default: 2024-02-01
    /// </summary>
    public string ApiVersion { get; set; } = "2024-02-01";
}

/// <summary>
/// Configuration options for Ollama connector in Semantic Kernel.
/// </summary>
public sealed class OllamaSemanticKernelOptions
{
    /// <summary>
    /// Ollama server base URL.
    /// Default: http://localhost:11434
    /// </summary>
    [Url(ErrorMessage = "Base URL must be a valid URL")]
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model ID to use (e.g., "llama3", "mistral", "codellama").
    /// </summary>
    [Required(ErrorMessage = "Ollama model ID is required")]
    public string ModelId { get; set; } = "llama3";
}

/// <summary>
/// Configuration options for Anthropic connector in Semantic Kernel.
/// </summary>
public sealed class AnthropicSemanticKernelOptions
{
    /// <summary>
    /// Anthropic API key.
    /// </summary>
    [Required(ErrorMessage = "Anthropic API key is required")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model ID to use (e.g., "claude-3-opus-20240229", "claude-3-sonnet-20240229").
    /// </summary>
    [Required(ErrorMessage = "Anthropic model ID is required")]
    public string ModelId { get; set; } = "claude-3-sonnet-20240229";

    /// <summary>
    /// Optional base URL override for Anthropic API.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Anthropic API version.
    /// Default: 2023-06-01
    /// </summary>
    public string ApiVersion { get; set; } = "2023-06-01";
}

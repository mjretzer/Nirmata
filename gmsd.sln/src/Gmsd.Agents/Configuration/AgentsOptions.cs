namespace Gmsd.Agents.Configuration;

/// <summary>
/// Configuration options for GMSD Agents (Plane layer).
/// </summary>
public sealed class AgentsOptions
{
    /// <summary>
    /// Configuration section name for Agents settings.
    /// </summary>
    public const string SectionName = "GmsdAgents";

    /// <summary>
    /// Maximum number of concurrent runs allowed.
    /// Default: 5
    /// </summary>
    public int MaxConcurrentRuns { get; set; } = 5;

    /// <summary>
    /// Default timeout for agent runs in minutes.
    /// Default: 30
    /// </summary>
    public int DefaultRunTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to enable detailed logging of LLM interactions.
    /// Default: false
    /// </summary>
    public bool LogLlmInteractions { get; set; } = false;

    /// <summary>
    /// Directory for prompt templates (overrides embedded resources if set).
    /// </summary>
    public string? PromptTemplateDirectory { get; set; }

    /// <summary>
    /// Whether to enable Git-related tools for subagents.
    /// Default: true
    /// </summary>
    public bool EnableGitTools { get; set; } = true;

    /// <summary>
    /// LLM-specific configuration options.
    /// </summary>
    public AgentsLlmOptions Llm { get; set; } = new();

    /// <summary>
    /// Observability configuration options.
    /// </summary>
    public AgentsObservabilityOptions Observability { get; set; } = new();
}

/// <summary>
/// LLM-specific configuration options within Agents settings.
/// </summary>
public sealed class AgentsLlmOptions
{
    /// <summary>
    /// The LLM provider to use.
    /// Valid values: openai, anthropic, azure-openai, ollama
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI-specific options (used when Provider = "openai").
    /// </summary>
    public OpenAiLlmOptions? OpenAI { get; set; }

    /// <summary>
    /// Anthropic-specific options (used when Provider = "anthropic").
    /// </summary>
    public AnthropicLlmOptions? Anthropic { get; set; }

    /// <summary>
    /// Azure OpenAI-specific options (used when Provider = "azure-openai").
    /// </summary>
    public AzureOpenAiLlmOptions? AzureOpenAI { get; set; }

    /// <summary>
    /// Ollama-specific options (used when Provider = "ollama").
    /// </summary>
    public OllamaLlmOptions? Ollama { get; set; }
}

/// <summary>
/// Observability configuration options for Agents.
/// </summary>
public sealed class AgentsObservabilityOptions
{
    /// <summary>
    /// Whether to enable correlation ID tracking.
    /// Default: true
    /// </summary>
    public bool EnableCorrelationIds { get; set; } = true;

    /// <summary>
    /// Prefix for run correlation IDs.
    /// Default: "RUN-"
    /// </summary>
    public string CorrelationIdPrefix { get; set; } = "RUN-";

    /// <summary>
    /// Whether to include correlation IDs in log output.
    /// Default: true
    /// </summary>
    public bool IncludeCorrelationIdInLogs { get; set; } = true;
}

using Gmsd.Agents.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Gmsd.Web.Pages.Settings;

/// <summary>
/// Page model for LLM configuration settings.
/// </summary>
public class LlmModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LlmModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmModel"/> class.
    /// </summary>
    public LlmModel(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<LlmModel> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    [BindProperty]
    public string? Provider { get; set; }

    // OpenAI
    [BindProperty]
    public string? OpenAiApiKey { get; set; }
    [BindProperty]
    public string? OpenAiModel { get; set; } = "gpt-4o";
    [BindProperty]
    public string? OpenAiBaseUrl { get; set; }

    // Anthropic
    [BindProperty]
    public string? AnthropicApiKey { get; set; }
    [BindProperty]
    public string? AnthropicModel { get; set; } = "claude-3-sonnet-20240229";

    // Azure OpenAI
    [BindProperty]
    public string? AzureEndpoint { get; set; }
    [BindProperty]
    public string? AzureApiKey { get; set; }
    [BindProperty]
    public string? AzureDeployment { get; set; }

    // Ollama
    [BindProperty]
    public string? OllamaBaseUrl { get; set; } = "http://localhost:11434";
    [BindProperty]
    public string? OllamaModel { get; set; } = "llama3";

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Loads current settings on page GET.
    /// </summary>
    public void OnGet()
    {
        try
        {
            // Load current values from configuration
            Provider = _configuration["Agents:Llm:Provider"];
            
            OpenAiModel = _configuration["Agents:Llm:OpenAI:DefaultModel"] ?? "gpt-4o";
            OpenAiBaseUrl = _configuration["Agents:Llm:OpenAI:BaseUrl"];
            // API keys are not loaded for security (masked)
            
            AnthropicModel = _configuration["Agents:Llm:Anthropic:DefaultModel"] ?? "claude-3-sonnet-20240229";
            
            AzureEndpoint = _configuration["Agents:Llm:AzureOpenAI:Endpoint"];
            AzureDeployment = _configuration["Agents:Llm:AzureOpenAI:DefaultDeployment"];
            
            OllamaBaseUrl = _configuration["Agents:Llm:Ollama:BaseUrl"] ?? "http://localhost:11434";
            OllamaModel = _configuration["Agents:Llm:Ollama:DefaultModel"] ?? "llama3";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading LLM settings");
            ErrorMessage = "Failed to load current settings.";
        }
    }

    /// <summary>
    /// Saves LLM settings to configuration.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Provider))
            {
                ErrorMessage = "Please select an LLM provider.";
                return Page();
            }

            // Validate provider-specific settings
            if (!ValidateProviderSettings(out var validationError))
            {
                ErrorMessage = validationError;
                return Page();
            }

            // Save settings to appsettings.json or user secrets
            await SaveSettingsAsync();

            SuccessMessage = "LLM settings saved successfully. The changes will take effect on the next request.";
            _logger.LogInformation("LLM settings updated. Provider: {Provider}", Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving LLM settings");
            ErrorMessage = "Failed to save settings: " + ex.Message;
        }

        return Page();
    }

    /// <summary>
    /// Tests the LLM connection with current settings.
    /// </summary>
    public async Task<IActionResult> OnPostTestAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Provider))
            {
                ErrorMessage = "Please select an LLM provider first.";
                return Page();
            }

            if (!ValidateProviderSettings(out var validationError))
            {
                ErrorMessage = validationError;
                return Page();
            }

            // For now, just simulate a test
            // In a real implementation, this would make a test API call
            SuccessMessage = $"Connection test initiated for {Provider}. In a production environment, this would verify API credentials.";
            _logger.LogInformation("LLM connection test for provider: {Provider}", Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing LLM connection");
            ErrorMessage = "Connection test failed: " + ex.Message;
        }

        return Page();
    }

    private bool ValidateProviderSettings(out string? error)
    {
        error = null;

        switch (Provider?.ToLowerInvariant())
        {
            case "openai":
                if (string.IsNullOrWhiteSpace(OpenAiApiKey) && string.IsNullOrWhiteSpace(_configuration["Agents:Llm:OpenAI:ApiKey"]))
                {
                    error = "OpenAI API key is required.";
                    return false;
                }
                break;

            case "anthropic":
                if (string.IsNullOrWhiteSpace(AnthropicApiKey) && string.IsNullOrWhiteSpace(_configuration["Agents:Llm:Anthropic:ApiKey"]))
                {
                    error = "Anthropic API key is required.";
                    return false;
                }
                break;

            case "azure-openai":
                if (string.IsNullOrWhiteSpace(AzureEndpoint))
                {
                    error = "Azure OpenAI endpoint is required.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(AzureApiKey) && string.IsNullOrWhiteSpace(_configuration["Agents:Llm:AzureOpenAI:ApiKey"]))
                {
                    error = "Azure OpenAI API key is required.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(AzureDeployment))
                {
                    error = "Azure OpenAI deployment name is required.";
                    return false;
                }
                break;

            case "ollama":
                if (string.IsNullOrWhiteSpace(OllamaBaseUrl))
                {
                    error = "Ollama base URL is required.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(OllamaModel))
                {
                    error = "Ollama model name is required.";
                    return false;
                }
                break;

            default:
                error = "Unknown provider selected.";
                return false;
        }

        return true;
    }

    private async Task SaveSettingsAsync()
    {
        // In a production app, this would save to user secrets or a secure settings store
        // For now, we'll update the in-memory configuration and optionally save to appsettings.Development.json
        
        var configPath = Path.Combine(_environment.ContentRootPath, "appsettings.Development.json");
        
        // Build the settings object
        var settings = new Dictionary<string, object?>();
        
        if (System.IO.File.Exists(configPath))
        {
            var json = await System.IO.File.ReadAllTextAsync(configPath);
            settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? settings;
        }

        // Ensure Agents:Llm section exists
        if (!settings.ContainsKey("Agents"))
        {
            settings["Agents"] = new Dictionary<string, object?>();
        }

        var agentsConfig = settings["Agents"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
        
        if (!agentsConfig.ContainsKey("Llm"))
        {
            agentsConfig["Llm"] = new Dictionary<string, object?>();
        }

        var llmConfig = agentsConfig["Llm"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
        llmConfig["Provider"] = Provider;

        // Provider-specific settings
        switch (Provider?.ToLowerInvariant())
        {
            case "openai":
                if (!llmConfig.ContainsKey("OpenAI"))
                    llmConfig["OpenAI"] = new Dictionary<string, object?>();
                var openAiConfig = llmConfig["OpenAI"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
                if (!string.IsNullOrWhiteSpace(OpenAiApiKey))
                    openAiConfig["ApiKey"] = OpenAiApiKey;
                openAiConfig["DefaultModel"] = OpenAiModel ?? "gpt-4o";
                if (!string.IsNullOrWhiteSpace(OpenAiBaseUrl))
                    openAiConfig["BaseUrl"] = OpenAiBaseUrl;
                break;

            case "anthropic":
                if (!llmConfig.ContainsKey("Anthropic"))
                    llmConfig["Anthropic"] = new Dictionary<string, object?>();
                var anthropicConfig = llmConfig["Anthropic"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
                if (!string.IsNullOrWhiteSpace(AnthropicApiKey))
                    anthropicConfig["ApiKey"] = AnthropicApiKey;
                anthropicConfig["DefaultModel"] = AnthropicModel ?? "claude-3-sonnet-20240229";
                break;

            case "azure-openai":
                if (!llmConfig.ContainsKey("AzureOpenAI"))
                    llmConfig["AzureOpenAI"] = new Dictionary<string, object?>();
                var azureConfig = llmConfig["AzureOpenAI"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
                azureConfig["Endpoint"] = AzureEndpoint;
                if (!string.IsNullOrWhiteSpace(AzureApiKey))
                    azureConfig["ApiKey"] = AzureApiKey;
                azureConfig["DefaultDeployment"] = AzureDeployment;
                break;

            case "ollama":
                if (!llmConfig.ContainsKey("Ollama"))
                    llmConfig["Ollama"] = new Dictionary<string, object?>();
                var ollamaConfig = llmConfig["Ollama"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
                ollamaConfig["BaseUrl"] = OllamaBaseUrl ?? "http://localhost:11434";
                ollamaConfig["DefaultModel"] = OllamaModel ?? "llama3";
                break;
        }

        // Save back to file
        agentsConfig["Llm"] = llmConfig;
        settings["Agents"] = agentsConfig;

        var options = new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        };
        var updatedJson = System.Text.Json.JsonSerializer.Serialize(settings, options);
        await System.IO.File.WriteAllTextAsync(configPath, updatedJson);
    }
}

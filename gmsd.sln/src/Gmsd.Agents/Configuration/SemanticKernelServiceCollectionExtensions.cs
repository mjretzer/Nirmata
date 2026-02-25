using Gmsd.Agents.Configuration;
using Gmsd.Agents.Execution.ControlPlane.Llm.Anthropic;
using Gmsd.Agents.Execution.ControlPlane.Llm.Tools;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Gmsd.Aos.Contracts.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
using System.ClientModel;
using System.Reflection;

namespace Gmsd.Agents.Configuration;

/// <summary>
/// Extension methods for registering Semantic Kernel services in the DI container.
/// </summary>
public static class SemanticKernelServiceCollectionExtensions
{
    /// <summary>
    /// Adds Semantic Kernel services with the configured LLM provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSemanticKernel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validate configuration at DI registration time
        ValidateSemanticKernelConfiguration(configuration);

        // Configure options for binding
        services.Configure<SemanticKernelOptions>(
            configuration.GetSection(SemanticKernelOptions.SectionName));

        // Register tool registry as singleton
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        // Register Kernel as a singleton since it holds state like plugins
        services.AddSingleton<Kernel>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<SemanticKernelOptions>>().Value;
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var toolRegistry = serviceProvider.GetService<IToolRegistry>();
            var httpClient = serviceProvider.GetService<HttpClient>();

            var builder = Kernel.CreateBuilder();

            if (loggerFactory is not null)
            {
                builder.Services.AddSingleton(loggerFactory);
            }

            // Add the configured provider-specific chat completion service
            var config = configuration.GetSection(SemanticKernelOptions.SectionName);
            AddProviderSpecificChatCompletion(builder, options, config, httpClient);

            // Register tools with the kernel if available
            if (toolRegistry is not null)
            {
                var plugin = Execution.ControlPlane.Llm.Tools.KernelPluginFactory.CreateFromRegistry(toolRegistry);
                if (plugin.FunctionCount > 0)
                {
                    builder.Plugins.Add(plugin);
                }
            }

            return builder.Build();
        });

        // Register IChatCompletionService as resolved from Kernel
        // This is the primary interface workflows should inject
        services.AddScoped<IChatCompletionService>(serviceProvider =>
        {
            var kernel = serviceProvider.GetRequiredService<Kernel>();
            return kernel.GetRequiredService<IChatCompletionService>();
        });

        return services;
    }

    /// <summary>
    /// Validates Semantic Kernel configuration at DI registration time.
    /// Throws InvalidOperationException with clear guidance on missing or invalid configuration.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid or missing.</exception>
    private static void ValidateSemanticKernelConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SemanticKernelOptions.SectionName);
        var options = section.Get<SemanticKernelOptions>();

        if (options is null)
        {
            throw new InvalidOperationException(
                $"Semantic Kernel configuration is missing. " +
                $"Ensure the '{SemanticKernelOptions.SectionName}' section is configured in appsettings.json. " +
                $"Example: {{\"{SemanticKernelOptions.SectionName}\": {{\"Provider\": \"OpenAi\", \"OpenAi\": {{\"ApiKey\": \"...\", \"ModelId\": \"gpt-4\"}}}}}}");
        }

        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            throw new InvalidOperationException(
                $"Semantic Kernel provider is required but not configured. " +
                $"Ensure '{SemanticKernelOptions.SectionName}:Provider' is set to one of: OpenAi, AzureOpenAi, Ollama, Anthropic. " +
                $"Example: {{\"{SemanticKernelOptions.SectionName}\": {{\"Provider\": \"OpenAi\"}}}}");
        }

        var providerName = options.Provider.ToLowerInvariant();

        switch (providerName)
        {
            case "openai":
                ValidateOpenAiConfiguration(options.OpenAi, section);
                break;
            case "azureopenai":
            case "azure-openai":
                ValidateAzureOpenAiConfiguration(options.AzureOpenAi, section);
                break;
            case "ollama":
                ValidateOllamaConfiguration(options.Ollama, section);
                break;
            case "anthropic":
                ValidateAnthropicConfiguration(options.Anthropic, section);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown LLM provider: '{options.Provider}'. " +
                    $"Valid values: OpenAi, AzureOpenAi, Ollama, Anthropic. " +
                    $"Ensure '{SemanticKernelOptions.SectionName}:Provider' is set to a valid provider name.");
        }
    }

    /// <summary>
    /// Validates OpenAI-specific configuration.
    /// </summary>
    private static void ValidateOpenAiConfiguration(OpenAiSemanticKernelOptions? options, IConfiguration section)
    {
        if (options is null)
        {
            throw new InvalidOperationException(
                "OpenAI configuration is missing. " +
                "Ensure 'GmsdAgents:SemanticKernel:OpenAi' section is configured with ApiKey and ModelId. " +
                "Example: {\"GmsdAgents\": {\"SemanticKernel\": {\"OpenAi\": {\"ApiKey\": \"sk-...\", \"ModelId\": \"gpt-4\"}}}}");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is required but not configured. " +
                "Ensure 'GmsdAgents:SemanticKernel:OpenAi:ApiKey' is set to your OpenAI API key. " +
                "You can obtain one from https://platform.openai.com/account/api-keys");
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            throw new InvalidOperationException(
                "OpenAI model ID is required but not configured. " +
                "Ensure 'GmsdAgents:SemanticKernel:OpenAi:ModelId' is set to a valid model (e.g., 'gpt-4', 'gpt-4-turbo', 'gpt-3.5-turbo'). " +
                "See https://platform.openai.com/docs/models for available models.");
        }

        var supportedModels = new[] { "gpt-4", "gpt-4-turbo", "gpt-4-turbo-preview", "gpt-4o", "gpt-3.5-turbo" };
        if (!supportedModels.Any(m => options.ModelId.StartsWith(m, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"OpenAI model '{options.ModelId}' is not in the list of supported models. " +
                $"Supported models: {string.Join(", ", supportedModels)}. " +
                $"See https://platform.openai.com/docs/models for available models.");
        }
    }

    /// <summary>
    /// Validates Azure OpenAI-specific configuration.
    /// </summary>
    private static void ValidateAzureOpenAiConfiguration(AzureOpenAiSemanticKernelOptions? options, IConfiguration section)
    {
        if (options is null)
        {
            throw new InvalidOperationException(
                "Azure OpenAI configuration is missing. " +
                "Ensure 'GmsdAgents:SemanticKernel:AzureOpenAi' section is configured with Endpoint, ApiKey, and DeploymentName. " +
                "Example: {\"GmsdAgents\": {\"SemanticKernel\": {\"AzureOpenAi\": {\"Endpoint\": \"https://...\", \"ApiKey\": \"...\", \"DeploymentName\": \"...\"}}}}");
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException(
                "Azure OpenAI endpoint is required but not configured. " +
                "Ensure 'GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint' is set to your Azure OpenAI endpoint URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Azure OpenAI API key is required but not configured. " +
                "Ensure 'GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey' is set to your Azure OpenAI API key.");
        }

        if (string.IsNullOrWhiteSpace(options.DeploymentName))
        {
            throw new InvalidOperationException(
                "Azure OpenAI deployment name is required but not configured. " +
                "Ensure 'GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName' is set to your deployment name.");
        }
    }

    /// <summary>
    /// Validates Ollama-specific configuration.
    /// </summary>
    private static void ValidateOllamaConfiguration(OllamaSemanticKernelOptions? options, IConfiguration section)
    {
        if (options is null)
        {
            options = new OllamaSemanticKernelOptions();
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            throw new InvalidOperationException(
                "Ollama model ID is required but not configured. " +
                "Ensure 'GmsdAgents:SemanticKernel:Ollama:ModelId' is set to a valid Ollama model (e.g., 'llama3', 'mistral', 'codellama'). " +
                "See https://ollama.ai/library for available models.");
        }
    }

    /// <summary>
    /// Validates Anthropic-specific configuration.
    /// </summary>
    private static void ValidateAnthropicConfiguration(AnthropicSemanticKernelOptions? options, IConfiguration section)
    {
        if (options is null)
        {
            throw new InvalidOperationException(
                "Anthropic configuration is missing. " +
                "Ensure 'GmsdAgents:SemanticKernel:Anthropic' section is configured with ApiKey and ModelId. " +
                "Example: {\"GmsdAgents\": {\"SemanticKernel\": {\"Anthropic\": {\"ApiKey\": \"sk-ant-...\", \"ModelId\": \"claude-3-opus-20240229\"}}}}");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Anthropic API key is required but not configured. " +
                "Ensure 'GmsdAgents:SemanticKernel:Anthropic:ApiKey' is set to your Anthropic API key. " +
                "You can obtain one from https://console.anthropic.com/account/keys");
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            throw new InvalidOperationException(
                "Anthropic model ID is required but not configured. " +
                "Ensure 'GmsdAgents:SemanticKernel:Anthropic:ModelId' is set to a valid model (e.g., 'claude-3-opus-20240229', 'claude-3-sonnet-20240229'). " +
                "See https://docs.anthropic.com/claude/reference/models-overview for available models.");
        }
    }

    /// <summary>
    /// Adds the provider-specific chat completion service to the kernel builder.
    /// </summary>
    private static void AddProviderSpecificChatCompletion(
        IKernelBuilder builder,
        SemanticKernelOptions options,
        IConfiguration configuration,
        HttpClient? httpClient = null)
    {
        var providerName = options.Provider.ToLowerInvariant();

        switch (providerName)
        {
            case "openai":
                builder.AddOpenAiChatCompletion(configuration, httpClient);
                break;
            case "azureopenai":
            case "azure-openai":
                builder.AddAzureOpenAiChatCompletion(configuration);
                break;
            case "ollama":
                builder.AddOllamaChatCompletion(configuration);
                break;
            case "anthropic":
                builder.AddAnthropicChatCompletion(configuration);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown LLM provider: {options.Provider}. " +
                    "Valid values: OpenAi, AzureOpenAi, Ollama, Anthropic");
        }
    }

    /// <summary>
    /// Adds OpenAI chat completion service to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="httpClient">Optional HttpClient to use (overrides internal creation).</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IKernelBuilder AddOpenAiChatCompletion(
        this IKernelBuilder builder,
        IConfiguration configuration,
        HttpClient? httpClient = null)
    {
        var options = configuration
            .GetSection("OpenAi")
            .Get<OpenAiSemanticKernelOptions>();

        if (options is null)
        {
            throw new InvalidOperationException(
                "OpenAI configuration is missing. " +
                "Ensure 'GmsdAgents:SemanticKernel:OpenAi' section is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is required. " +
                "Ensure 'GmsdAgents:SemanticKernel:OpenAi:ApiKey' is set.");
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            throw new InvalidOperationException(
                "OpenAI model ID is required. " +
                "Ensure 'GmsdAgents:SemanticKernel:OpenAi:ModelId' is set.");
        }

        // Map configuration to OpenAIPromptExecutionSettings
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ModelId = options.ModelId,
            Temperature = options.Temperature,
            MaxTokens = options.MaxTokens,
            TopP = options.TopP,
            FrequencyPenalty = options.FrequencyPenalty,
            PresencePenalty = options.PresencePenalty,
            Seed = options.Seed,
            ToolCallBehavior = options.EnableParallelToolCalls
                ? ToolCallBehavior.AutoInvokeKernelFunctions
                : ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // Register the execution settings in the kernel services
        builder.Services.AddSingleton(executionSettings);

        if (httpClient != null)
        {
            // Create OpenAIClient with custom transport using the injected HttpClient
            var openAiClientOptions = new OpenAIClientOptions
            {
                Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(httpClient)
            };
            
            var openAiClient = new OpenAIClient(new ApiKeyCredential(options.ApiKey), openAiClientOptions);
            
            builder.AddOpenAIChatCompletion(
                modelId: options.ModelId,
                openAIClient: openAiClient);
        }
        else if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            // If BaseUrl is provided but no HttpClient, create one with the correct BaseAddress
            var customHttpClient = new HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl)
            };
            
            var openAiClientOptions = new OpenAIClientOptions
            {
                Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(customHttpClient)
            };

            var openAiClient = new OpenAIClient(new ApiKeyCredential(options.ApiKey), openAiClientOptions);

            builder.AddOpenAIChatCompletion(
                modelId: options.ModelId,
                openAIClient: openAiClient);
        }
        else
        {
            builder.AddOpenAIChatCompletion(
                modelId: options.ModelId,
                apiKey: options.ApiKey,
                orgId: options.OrganizationId);
        }

        return builder;
    }

    /// <summary>
    /// Adds Azure OpenAI chat completion service to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IKernelBuilder AddAzureOpenAiChatCompletion(
        this IKernelBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("AzureOpenAi")
            .Get<AzureOpenAiSemanticKernelOptions>();

        if (options is null)
        {
            throw new InvalidOperationException(
                "Azure OpenAI configuration is missing. " +
                "Ensure 'GmsdAgents:SemanticKernel:AzureOpenAi' section is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException(
                "Azure OpenAI endpoint is required. " +
                "Ensure 'GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint' is set.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Azure OpenAI API key is required. " +
                "Ensure 'GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey' is set.");
        }

        if (string.IsNullOrWhiteSpace(options.DeploymentName))
        {
            throw new InvalidOperationException(
                "Azure OpenAI deployment name is required. " +
                "Ensure 'GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName' is set.");
        }

        // Map configuration to AzureOpenAIPromptExecutionSettings
        var executionSettings = new AzureOpenAIPromptExecutionSettings();

        // Register the execution settings in the kernel services
        builder.Services.AddSingleton(executionSettings);

        builder.AddAzureOpenAIChatCompletion(
            deploymentName: options.DeploymentName,
            endpoint: options.Endpoint,
            apiKey: options.ApiKey,
            apiVersion: options.ApiVersion);

        return builder;
    }

    /// <summary>
    /// Adds Ollama chat completion service to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IKernelBuilder AddOllamaChatCompletion(
        this IKernelBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("Ollama")
            .Get<OllamaSemanticKernelOptions>();

        if (options is null)
        {
            // Use defaults if configuration is missing
            options = new OllamaSemanticKernelOptions();
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            throw new InvalidOperationException(
                "Ollama model ID is required. " +
                "Ensure 'GmsdAgents:SemanticKernel:Ollama:ModelId' is set.");
        }

        // Ollama connector is currently in preview (SKEXP0070)
#pragma warning disable SKEXP0070
        builder.AddOllamaChatCompletion(
            modelId: options.ModelId,
            endpoint: new System.Uri(options.BaseUrl));
#pragma warning restore SKEXP0070

        return builder;
    }

    /// <summary>
    /// Adds Anthropic chat completion service to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IKernelBuilder AddAnthropicChatCompletion(
        this IKernelBuilder builder,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("Anthropic")
            .Get<AnthropicSemanticKernelOptions>();

        if (options is null)
        {
            throw new InvalidOperationException(
                "Anthropic configuration is missing. " +
                "Ensure 'GmsdAgents:SemanticKernel:Anthropic' section is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Anthropic API key is required. " +
                "Ensure 'GmsdAgents:SemanticKernel:Anthropic:ApiKey' is set.");
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            throw new InvalidOperationException(
                "Anthropic model ID is required. " +
                "Ensure 'GmsdAgents:SemanticKernel:Anthropic:ModelId' is set.");
        }

        // Register default execution settings
        var executionSettings = new ClaudePromptExecutionSettings
        {
            ModelId = options.ModelId,
            AnthropicVersion = options.ApiVersion
        };
        builder.Services.AddSingleton(executionSettings);

        // Register HttpClient for Anthropic
        var baseUrl = options.BaseUrl ?? "https://api.anthropic.com";
        builder.Services.AddSingleton<HttpClient>(serviceProvider =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };
            return httpClient;
        });

        // Register the Anthropic chat completion service
        builder.Services.AddSingleton<IChatCompletionService>(serviceProvider =>
        {
            var config = new AnthropicServiceConfig
            {
                ApiKey = options.ApiKey,
                ModelId = options.ModelId,
                BaseUrl = options.BaseUrl
            };

            var httpClient = serviceProvider.GetRequiredService<HttpClient>();
            var logger = serviceProvider.GetService<ILogger<AnthropicChatCompletionService>>();

            return new AnthropicChatCompletionService(config, httpClient, logger);
        });

        return builder;
    }

    /// <summary>
    /// Scans the specified assemblies for ITool implementations and registers them with the tool registry.
    /// Tools must have a parameterless constructor and a Descriptor property.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for tool implementations. If empty, scans the entry and calling assemblies.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ScanAndRegisterTools(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var assembliesToScan = assemblies.Length > 0
            ? assemblies
            : new[] { Assembly.GetEntryAssembly(), Assembly.GetCallingAssembly() }
                .Where(a => a is not null)
                .Cast<Assembly>()
                .ToArray();

        foreach (var assembly in assembliesToScan)
        {
            var toolTypes = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false })
                .Where(t => t.GetInterfaces().Any(i => i == typeof(ITool)));

            foreach (var toolType in toolTypes)
            {
                services.AddTransient(typeof(ITool), toolType);
            }
        }

        return services;
    }

    /// <summary>
    /// Registers a specific ITool implementation with the tool registry.
    /// The tool must have a Descriptor property accessible via reflection.
    /// </summary>
    /// <typeparam name="TTool">The tool implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterTool<TTool>(this IServiceCollection services)
        where TTool : class, ITool
    {
        services.AddTransient<ITool, TTool>();
        return services;
    }

    /// <summary>
    /// Registers all ITool instances from the DI container with the tool registry.
    /// This should be called after all tool implementations have been registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection InitializeToolRegistry(this IServiceCollection services)
    {
        services.AddSingleton(serviceProvider =>
        {
            var registry = serviceProvider.GetRequiredService<IToolRegistry>();
            var tools = serviceProvider.GetServices<ITool>();

            foreach (var tool in tools)
            {
                var descriptor = GetToolDescriptor(tool);
                if (descriptor is not null)
                {
                    registry.Register(descriptor, tool);
                }
            }

            return registry;
        });

        return services;
    }

    /// <summary>
    /// Attempts to extract the ToolDescriptor from an ITool instance using reflection.
    /// </summary>
    private static ToolDescriptor? GetToolDescriptor(ITool tool)
    {
        // First try: look for a Descriptor property directly
        var property = tool.GetType().GetProperty("Descriptor");
        if (property?.PropertyType == typeof(ToolDescriptor))
        {
            return property.GetValue(tool) as ToolDescriptor;
        }

        // Second try: look for an interface that exposes descriptor
        var interfaceType = tool.GetType().GetInterfaces()
            .FirstOrDefault(i => i.GetProperty("Descriptor")?.PropertyType == typeof(ToolDescriptor));

        if (interfaceType is not null)
        {
            var descriptorProperty = interfaceType.GetProperty("Descriptor");
            return descriptorProperty?.GetValue(tool) as ToolDescriptor;
        }

        return null;
    }
}

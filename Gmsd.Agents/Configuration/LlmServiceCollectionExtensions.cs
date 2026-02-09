using Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.Anthropic;
using Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.AzureOpenAi;
using Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.Ollama;
using Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.OpenAi;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gmsd.Agents.Configuration;

/// <summary>
/// Extension methods for registering LLM providers in the DI container.
/// </summary>
public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Adds LLM provider services based on configuration.
    /// </summary>
    public static IServiceCollection AddLlmProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var providerName = configuration["Agents:Llm:Provider"]
            ?? throw new InvalidOperationException(
                "Agents:Llm:Provider configuration is required. " +
                "Valid values: openai, anthropic, azure-openai, ollama");

        return providerName.ToLowerInvariant() switch
        {
            "openai" => services.AddOpenAiLlm(configuration),
            "anthropic" => services.AddAnthropicLlm(configuration),
            "azure-openai" => services.AddAzureOpenAiLlm(configuration),
            "ollama" => services.AddOllamaLlm(configuration),
            _ => throw new InvalidOperationException(
                $"Unknown LLM provider: {providerName}. " +
                "Valid values: openai, anthropic, azure-openai, ollama")
        };
    }

    /// <summary>
    /// Registers OpenAI as the LLM provider.
    /// </summary>
    public static IServiceCollection AddOpenAiLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpenAiLlmOptions>(
            configuration.GetSection("Agents:Llm:OpenAI"));
        services.AddSingleton<ILlmProvider, OpenAiLlmAdapter>();
        return services;
    }

    /// <summary>
    /// Registers Anthropic as the LLM provider.
    /// </summary>
    public static IServiceCollection AddAnthropicLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AnthropicLlmOptions>(
            configuration.GetSection("Agents:Llm:Anthropic"));
        services.AddSingleton<ILlmProvider, AnthropicLlmAdapter>();
        return services;
    }

    /// <summary>
    /// Registers Azure OpenAI as the LLM provider.
    /// </summary>
    public static IServiceCollection AddAzureOpenAiLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AzureOpenAiLlmOptions>(
            configuration.GetSection("Agents:Llm:AzureOpenAI"));
        services.AddSingleton<ILlmProvider, AzureOpenAiLlmAdapter>();
        return services;
    }

    /// <summary>
    /// Registers Ollama as the LLM provider.
    /// </summary>
    public static IServiceCollection AddOllamaLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OllamaLlmOptions>(
            configuration.GetSection("Agents:Llm:Ollama"));
        services.AddSingleton<ILlmProvider, OllamaLlmAdapter>();
        return services;
    }
}

#pragma warning disable CS0618 // Intentionally using obsolete ILlmProvider during migration period

using nirmata.Agents.Execution.ControlPlane.Llm.Adapters;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace nirmata.Agents.Configuration;

/// <summary>
/// Extension methods for registering LLM providers in the DI container.
/// </summary>
public static class LlmServiceCollectionExtensions
{
    private const string LegacyProviderKey = "Agents:Llm:Provider";
    private const string LegacySectionPrefix = "Agents:Llm:";

    /// <summary>
    /// Adds LLM provider services using Semantic Kernel.
    /// </summary>
    public static IServiceCollection AddLlmProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Check for legacy configuration and migrate if present
        var configurationToUse = MigrateLegacyConfigurationIfPresent(services, configuration);

        // First, add Semantic Kernel services (registers Kernel and IChatCompletionService)
        services.AddSemanticKernel(configurationToUse);

        // Register the adapter that implements ILlmProvider over IChatCompletionService
        services.AddSingleton<ILlmProvider, SemanticKernelLlmProvider>();

        // Register LLM interceptors for observability and safety
        services.AddSingleton<ILlmInterceptor, LlmLoggingInterceptor>();
        services.AddSingleton<ILlmInterceptor, LlmSafetyInterceptor>();

        return services;
    }

    /// <summary>
    /// Checks for legacy configuration at Agents:Llm:Provider and migrates to new path.
    /// Logs a warning when legacy configuration is detected.
    /// </summary>
    private static IConfiguration MigrateLegacyConfigurationIfPresent(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var legacyProvider = configuration[LegacyProviderKey];
        if (string.IsNullOrWhiteSpace(legacyProvider))
        {
            // No legacy configuration found, use configuration as-is
            return configuration;
        }

        // Legacy configuration detected - create a logger to warn about deprecation
        var loggerFactory = services.BuildServiceProvider().GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("LlmServiceCollectionExtensions");
        logger?.LogWarning(
            "Legacy configuration detected at '{LegacyPath}'. " +
            "Please migrate to '{NewPath}'. " +
            "Legacy support will be removed in a future release.",
            LegacyProviderKey,
            SemanticKernelOptions.SectionName + ":Provider");

        // Build a new configuration that maps legacy values to the new section
        var migratedValues = new Dictionary<string, string?>();

        // Map the provider name
        migratedValues[$"{SemanticKernelOptions.SectionName}:Provider"] = legacyProvider;

        // Map provider-specific settings from legacy section to new section
        var legacySection = configuration.GetSection(LegacySectionPrefix);
        var providerSection = legacySection.GetSection(legacyProvider);

        foreach (var child in providerSection.GetChildren())
        {
            var newKey = $"{SemanticKernelOptions.SectionName}:{legacyProvider}:{child.Key}";
            migratedValues[newKey] = child.Value;
        }

        // Create a new configuration builder that includes both original and migrated values
        // The migrated values take precedence for the SemanticKernel section
        return new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(migratedValues)
            .Build();
    }
}

#pragma warning restore CS0618


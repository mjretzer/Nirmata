using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Gmsd.Aos.Configuration;
using Gmsd.Aos.Contracts.Secrets;
using Gmsd.Aos.Engine.Secrets;

namespace Gmsd.Aos.Composition;

/// <summary>
/// Extension methods for registering secret management services.
/// </summary>
public static class SecretServiceCollectionExtensions
{
    /// <summary>
    /// Register secret management services in the dependency injection container.
    /// </summary>
    public static IServiceCollection AddSecretManagement(
        this IServiceCollection services,
        SecretConfigurationOptions? options = null)
    {
        options ??= new SecretConfigurationOptions();

        services.AddSingleton(options);

        // Register the appropriate secret store implementation based on configuration
        if (options.StoreType == "mock" || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<ISecretStore, MockSecretStore>();
        }
        else
        {
            services.AddSingleton<ISecretStore, WindowsCredentialManagerSecretStore>();
        }

        // Register the configuration resolver
        services.AddSingleton<SecretConfigurationResolver>(sp =>
            new SecretConfigurationResolver(sp.GetRequiredService<ISecretStore>()));

        return services;
    }

    /// <summary>
    /// Register secret management services with custom options.
    /// </summary>
    public static IServiceCollection AddSecretManagement(
        this IServiceCollection services,
        Action<SecretConfigurationOptions> configureOptions)
    {
        var options = new SecretConfigurationOptions();
        configureOptions(options);
        return services.AddSecretManagement(options);
    }
}

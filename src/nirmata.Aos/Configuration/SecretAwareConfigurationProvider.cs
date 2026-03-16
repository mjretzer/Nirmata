using nirmata.Aos.Contracts.Secrets;
using nirmata.Aos.Engine.Secrets;

namespace nirmata.Aos.Configuration;

/// <summary>
/// Configuration provider that resolves secret references in configuration files.
/// Handles loading configuration with $secret:name references and resolving them at load time.
/// </summary>
public class SecretAwareConfigurationProvider
{
    private readonly ISecretStore _secretStore;
    private readonly SecretConfigurationOptions _options;

    public SecretAwareConfigurationProvider(
        ISecretStore secretStore,
        SecretConfigurationOptions options)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Load configuration from a file and resolve secret references.
    /// </summary>
    /// <param name="configFilePath">Path to the configuration file</param>
    /// <returns>The configuration with secret references resolved</returns>
    public async Task<string> LoadAndResolveAsync(string configFilePath)
    {
        if (!File.Exists(configFilePath))
            throw new FileNotFoundException($"Configuration file not found: {configFilePath}");

        var configJson = await File.ReadAllTextAsync(configFilePath);

        if (!_options.EnableSecretResolution)
            return configJson;

        var resolver = new SecretConfigurationResolver(_secretStore);
        return await resolver.ResolveAsync(configJson);
    }

    /// <summary>
    /// Load configuration from a string and resolve secret references.
    /// </summary>
    /// <param name="configJson">The configuration as a JSON string</param>
    /// <returns>The configuration with secret references resolved</returns>
    public async Task<string> ResolveAsync(string configJson)
    {
        if (!_options.EnableSecretResolution)
            return configJson;

        var resolver = new SecretConfigurationResolver(_secretStore);
        return await resolver.ResolveAsync(configJson);
    }

    /// <summary>
    /// Get a safe version of configuration for logging (with secrets masked).
    /// </summary>
    /// <param name="configJson">The configuration as a JSON string</param>
    /// <returns>The configuration with secret values masked</returns>
    public static string GetSafeForLogging(string configJson)
    {
        return SecretConfigurationResolver.MaskSecrets(configJson);
    }

    /// <summary>
    /// Check if configuration contains secret references.
    /// </summary>
    /// <param name="configJson">The configuration as a JSON string</param>
    /// <returns>True if the configuration contains secret references</returns>
    public static bool ContainsSecretReferences(string configJson)
    {
        return SecretConfigurationResolver.ContainsSecretReferences(configJson);
    }
}

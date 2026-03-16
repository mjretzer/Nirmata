namespace nirmata.Aos.Configuration;

/// <summary>
/// Configuration options for secret management.
/// Supports $secret:name references in configuration values.
/// </summary>
public class SecretConfigurationOptions
{
    /// <summary>
    /// Gets or sets the secret store type to use.
    /// Valid values: "windows-credential-manager", "mock" (for testing)
    /// Default: "windows-credential-manager" on Windows, "mock" otherwise
    /// </summary>
    public string StoreType { get; set; } = "windows-credential-manager";

    /// <summary>
    /// Gets or sets whether to enable automatic secret reference resolution.
    /// When enabled, configuration values containing $secret:name are automatically resolved.
    /// Default: true
    /// </summary>
    public bool EnableSecretResolution { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log secret references (not values) for debugging.
    /// When enabled, logs will show $secret:name instead of actual values.
    /// Default: true
    /// </summary>
    public bool LogSecretReferences { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for secret store operations in milliseconds.
    /// Default: 5000 (5 seconds)
    /// </summary>
    public int SecretStoreTimeoutMs { get; set; } = 5000;
}

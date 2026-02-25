namespace Gmsd.Aos.Contracts.Secrets;

/// <summary>
/// Abstraction for secure credential storage and retrieval.
/// Implementations must never log secret values and must provide clear error messages.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Store a secret by name.
    /// </summary>
    /// <param name="name">The secret name (e.g., "openai-key")</param>
    /// <param name="value">The secret value</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SetSecretAsync(string name, string value);

    /// <summary>
    /// Retrieve a secret by name.
    /// </summary>
    /// <param name="name">The secret name</param>
    /// <returns>The secret value</returns>
    /// <exception cref="SecretNotFoundException">Thrown if the secret does not exist</exception>
    Task<string> GetSecretAsync(string name);

    /// <summary>
    /// Delete a secret by name.
    /// </summary>
    /// <param name="name">The secret name</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="SecretNotFoundException">Thrown if the secret does not exist</exception>
    Task DeleteSecretAsync(string name);

    /// <summary>
    /// Check if a secret exists.
    /// </summary>
    /// <param name="name">The secret name</param>
    /// <returns>True if the secret exists, false otherwise</returns>
    Task<bool> SecretExistsAsync(string name);

    /// <summary>
    /// List all secret names (not values).
    /// </summary>
    /// <returns>A collection of secret names</returns>
    Task<IReadOnlyCollection<string>> ListSecretsAsync();
}

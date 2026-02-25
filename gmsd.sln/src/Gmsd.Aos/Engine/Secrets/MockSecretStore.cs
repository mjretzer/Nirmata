using Gmsd.Aos.Contracts.Secrets;

namespace Gmsd.Aos.Engine.Secrets;

/// <summary>
/// In-memory mock implementation of ISecretStore for testing.
/// Stores secrets in memory without persistence.
/// </summary>
public class MockSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public Task SetSecretAsync(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Secret value cannot be null or empty.", nameof(value));

        lock (_lock)
        {
            _secrets[name] = value;
        }

        return Task.CompletedTask;
    }

    public Task<string> GetSecretAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(name));

        lock (_lock)
        {
            if (!_secrets.TryGetValue(name, out var value))
                throw new SecretNotFoundException(name);

            return Task.FromResult(value);
        }
    }

    public Task DeleteSecretAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(name));

        lock (_lock)
        {
            if (!_secrets.Remove(name))
                throw new SecretNotFoundException(name);
        }

        return Task.CompletedTask;
    }

    public Task<bool> SecretExistsAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be null or empty.", nameof(name));

        lock (_lock)
        {
            return Task.FromResult(_secrets.ContainsKey(name));
        }
    }

    public Task<IReadOnlyCollection<string>> ListSecretsAsync()
    {
        lock (_lock)
        {
            var names = _secrets.Keys.ToList().AsReadOnly();
            return Task.FromResult((IReadOnlyCollection<string>)names);
        }
    }

    /// <summary>
    /// For testing only: Clear all secrets from the mock store.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _secrets.Clear();
        }
    }

    /// <summary>
    /// For testing only: Get the count of stored secrets.
    /// </summary>
    public int GetSecretCount()
    {
        lock (_lock)
        {
            return _secrets.Count;
        }
    }
}

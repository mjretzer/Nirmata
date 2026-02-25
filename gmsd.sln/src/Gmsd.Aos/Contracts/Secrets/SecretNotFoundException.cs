namespace Gmsd.Aos.Contracts.Secrets;

/// <summary>
/// Thrown when a secret is not found in the secret store.
/// </summary>
public class SecretNotFoundException : Exception
{
    public SecretNotFoundException(string secretName)
        : base($"Secret '{secretName}' not found in secret store.")
    {
        SecretName = secretName;
    }

    public string SecretName { get; }
}

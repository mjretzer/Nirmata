namespace nirmata.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Exception thrown when an LLM provider operation fails.
/// </summary>
public class LlmProviderException : Exception
{
    /// <summary>
    /// The name of the provider that threw the exception.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Optional error code from the provider.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Indicates whether the error is retryable.
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderException"/> class.
    /// </summary>
    public LlmProviderException(
        string providerName,
        string message,
        string? errorCode = null,
        bool isRetryable = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
    }
}

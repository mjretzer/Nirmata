namespace nirmata.Aos.Engine.Errors;

/// <summary>
/// Raised when an operation violates an explicit policy gate (e.g., output scope restrictions).
/// </summary>
internal sealed class AosPolicyViolationException : Exception
{
    public AosPolicyViolationException(string message)
        : base(message)
    {
    }

    public AosPolicyViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}


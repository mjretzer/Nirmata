namespace Gmsd.Agents.Execution.ControlPlane.Tools.Firewall;

/// <summary>
/// Exception thrown when a tool attempts to access a file outside the allowed scope.
/// </summary>
public sealed class ScopeViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeViolationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ScopeViolationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeViolationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ScopeViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

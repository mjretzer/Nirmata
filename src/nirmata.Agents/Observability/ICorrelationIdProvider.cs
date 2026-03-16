namespace nirmata.Agents.Observability;

/// <summary>
/// Provides correlation IDs for tracing agent runs.
/// </summary>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// Generates a new correlation ID.
    /// </summary>
    string Generate();

    /// <summary>
    /// Gets the current correlation ID for the execution context.
    /// </summary>
    string Current { get; }

    /// <summary>
    /// Sets the current correlation ID.
    /// </summary>
    void SetCurrent(string correlationId);
}

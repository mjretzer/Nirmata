namespace Gmsd.Agents.Observability;

/// <summary>
/// Provides correlation ID management for request tracing.
/// Implements the ICorrelationIdProvider interface for consistent ID generation and propagation.
/// </summary>
public class CorrelationIdProvider : ICorrelationIdProvider
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    public string Generate()
    {
        return Guid.NewGuid().ToString("N");
    }

    public string Current
    {
        get
        {
            if (CurrentCorrelationId.Value == null)
            {
                CurrentCorrelationId.Value = Generate();
            }
            return CurrentCorrelationId.Value;
        }
    }

    public void SetCurrent(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation ID cannot be null or empty", nameof(correlationId));

        CurrentCorrelationId.Value = correlationId;
    }

    public string? GetCorrelationId()
    {
        return CurrentCorrelationId.Value;
    }

    public void ClearCorrelationId()
    {
        CurrentCorrelationId.Value = null;
    }
}

using nirmata.Agents.Configuration;
using Microsoft.Extensions.Options;

namespace nirmata.Agents.Observability;

/// <summary>
/// Correlation ID provider that formats IDs as RUN-*.
/// </summary>
public sealed class RunCorrelationIdProvider : ICorrelationIdProvider
{
    private readonly AgentsObservabilityOptions _options;
    private string _currentCorrelationId = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunCorrelationIdProvider"/> class.
    /// </summary>
    public RunCorrelationIdProvider(IOptions<AgentsOptions> options)
    {
        _options = options.Value.Observability;
    }

    /// <inheritdoc />
    public string Generate()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = new Random().Next(1000, 9999);
        return $"{_options.CorrelationIdPrefix}{timestamp}-{random}";
    }

    /// <inheritdoc />
    public string Current => string.IsNullOrEmpty(_currentCorrelationId) ? Generate() : _currentCorrelationId;

    /// <inheritdoc />
    public void SetCurrent(string correlationId)
    {
        _currentCorrelationId = correlationId;
    }
}

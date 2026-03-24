using Microsoft.Extensions.Logging;

namespace nirmata.Windows.Service.Api;

/// <summary>
/// Captures ASP.NET Core log messages into <see cref="DaemonRuntimeState"/>'s
/// circular buffer so they can be served by <c>GET /api/v1/logs</c>.
/// </summary>
public sealed class DaemonLogSink(DaemonRuntimeState state) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CategoryLogger(state, categoryName);

    public void Dispose() { }

    private sealed class CategoryLogger(DaemonRuntimeState state, string source) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState value,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(value, exception);
            if (exception is not null)
                message = $"{message}\n{exception}";

            state.AddLogEntry(new HostLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = logLevel.ToString().ToLowerInvariant(),
                Message = message,
                Source = source,
            });
        }
    }
}

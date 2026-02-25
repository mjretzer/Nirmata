using System.Text.Json;
using Gmsd.Agents.Execution.ToolCalling;

namespace Gmsd.Agents.Execution.Evidence;

/// <summary>
/// Captures tool calling events to a structured NDJSON log file.
/// Each line is a JSON object representing one tool call and its result.
/// </summary>
public sealed class ToolCallingEventLogger : IToolCallingEventEmitter
{
    private readonly string _logFilePath;
    private readonly object _lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCallingEventLogger"/> class.
    /// </summary>
    /// <param name="logFilePath">Path to the NDJSON log file.</param>
    public ToolCallingEventLogger(string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);
        _logFilePath = logFilePath;
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <inheritdoc />
    public void Emit(ToolCallingEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        try
        {
            var logEntry = new
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                eventType = @event.GetType().Name,
                data = @event
            };

            var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });

            lock (_lockObject)
            {
                File.AppendAllText(_logFilePath, json + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // Log to console if file write fails
            Console.Error.WriteLine($"Failed to write tool calling event log: {ex.Message}");
        }
    }
}

namespace Gmsd.Aos.Tests.E2E.Harness;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Provides utilities for reading event logs from the .aos/state/events.jsonl file.
/// </summary>
public static class EventLogReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Reads the last N events from the events log.
    /// </summary>
    /// <param name="repoRoot">The root path of the repository.</param>
    /// <param name="count">The number of events to read from the tail.</param>
    /// <returns>A list of event entries, most recent first.</returns>
    public static IReadOnlyList<EventEntry> ReadTail(string repoRoot, int count)
    {
        var eventsPath = Path.Combine(repoRoot, ".aos", "state", "events.jsonl");
        
        if (!File.Exists(eventsPath))
        {
            return new List<EventEntry>();
        }

        var lines = File.ReadAllLines(eventsPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var events = new List<EventEntry>();
        
        // Read last N lines
        var linesToRead = Math.Min(count, lines.Count);
        for (var i = lines.Count - linesToRead; i < lines.Count; i++)
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(lines[i], JsonOptions);
                var eventType = json.GetProperty("eventType").GetString() ?? "unknown";
                var timestamp = json.TryGetProperty("timestamp", out var ts) 
                    ? ts.GetDateTimeOffset() 
                    : DateTimeOffset.UtcNow;
                
                events.Add(new EventEntry(eventType, timestamp, json));
            }
            catch
            {
                // Skip malformed lines
            }
        }

        // Return in reverse chronological order (most recent first)
        events.Reverse();
        return events;
    }

    /// <summary>
    /// Reads all events from the events log.
    /// </summary>
    /// <param name="repoRoot">The root path of the repository.</param>
    /// <returns>A list of all event entries.</returns>
    public static IReadOnlyList<EventEntry> ReadAll(string repoRoot)
    {
        var eventsPath = Path.Combine(repoRoot, ".aos", "state", "events.jsonl");
        
        if (!File.Exists(eventsPath))
        {
            return new List<EventEntry>();
        }

        var lines = File.ReadAllLines(eventsPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var events = new List<EventEntry>();
        
        foreach (var line in lines)
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(line, JsonOptions);
                var eventType = json.GetProperty("eventType").GetString() ?? "unknown";
                var timestamp = json.TryGetProperty("timestamp", out var ts) 
                    ? ts.GetDateTimeOffset() 
                    : DateTimeOffset.UtcNow;
                
                events.Add(new EventEntry(eventType, timestamp, json));
            }
            catch
            {
                // Skip malformed lines
            }
        }

        return events;
    }
}

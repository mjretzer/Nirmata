using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Tools for inspecting and debugging streaming events.
/// Provides utilities for event analysis, filtering, and visualization.
/// </summary>
public class EventInspectionTools
{
    private readonly JsonSerializerOptions _jsonOptions;

    public EventInspectionTools()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Formats an event for display.
    /// </summary>
    public string FormatEvent(StreamingEvent @event)
    {
        var lines = new List<string>
        {
            $"Event: {GetEventTypeName(@event.Type)}",
            $"ID: {@event.Id}",
            $"Timestamp: {@event.Timestamp:O}",
            $"Correlation ID: {@event.CorrelationId ?? "N/A"}",
            $"Sequence Number: {@event.SequenceNumber?.ToString() ?? "N/A"}"
        };

        if (@event.Payload != null)
        {
            lines.Add($"Payload Type: {@event.Payload.GetType().Name}");
            lines.Add("Payload:");
            var payloadJson = JsonSerializer.Serialize(@event.Payload, _jsonOptions);
            foreach (var line in payloadJson.Split('\n'))
            {
                lines.Add($"  {line}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Filters events by type.
    /// </summary>
    public IEnumerable<StreamingEvent> FilterByType(IEnumerable<StreamingEvent> events, StreamingEventType type)
    {
        return events.Where(e => e.Type == type);
    }

    /// <summary>
    /// Filters events by correlation ID.
    /// </summary>
    public IEnumerable<StreamingEvent> FilterByCorrelationId(IEnumerable<StreamingEvent> events, string correlationId)
    {
        return events.Where(e => e.CorrelationId == correlationId);
    }

    /// <summary>
    /// Filters events by sequence number range.
    /// </summary>
    public IEnumerable<StreamingEvent> FilterBySequenceRange(IEnumerable<StreamingEvent> events, long minSequence, long maxSequence)
    {
        return events.Where(e => e.SequenceNumber.HasValue && e.SequenceNumber >= minSequence && e.SequenceNumber <= maxSequence);
    }

    /// <summary>
    /// Analyzes event sequence for completeness.
    /// </summary>
    public EventSequenceAnalysis AnalyzeSequence(IEnumerable<StreamingEvent> events)
    {
        var eventList = events.ToList();
        var analysis = new EventSequenceAnalysis
        {
            TotalEvents = eventList.Count,
            EventsByType = new Dictionary<string, int>(),
            SequenceGaps = new List<long>(),
            CorrelationIds = new HashSet<string>()
        };

        // Count events by type
        foreach (var @event in eventList)
        {
            var typeName = GetEventTypeName(@event.Type);
            if (!analysis.EventsByType.ContainsKey(typeName))
                analysis.EventsByType[typeName] = 0;
            analysis.EventsByType[typeName]++;

            if (@event.CorrelationId != null)
                analysis.CorrelationIds.Add(@event.CorrelationId);
        }

        // Check for sequence gaps
        var sequenceNumbers = eventList
            .Where(e => e.SequenceNumber.HasValue)
            .OrderBy(e => e.SequenceNumber)
            .Select(e => e.SequenceNumber!.Value)
            .ToList();

        for (int i = 1; i < sequenceNumbers.Count; i++)
        {
            if (sequenceNumbers[i] != sequenceNumbers[i - 1] + 1)
            {
                analysis.SequenceGaps.Add(sequenceNumbers[i - 1]);
            }
        }

        return analysis;
    }

    /// <summary>
    /// Generates a timeline view of events.
    /// </summary>
    public string GenerateTimeline(IEnumerable<StreamingEvent> events)
    {
        var sortedEvents = events.OrderBy(e => e.Timestamp).ToList();
        var lines = new List<string> { "Event Timeline:" };

        foreach (var @event in sortedEvents)
        {
            var typeName = GetEventTypeName(@event.Type);
            var duration = @event.Timestamp.ToString("HH:mm:ss.fff");
            lines.Add($"[{duration}] {typeName} (seq: {@event.SequenceNumber?.ToString() ?? "N/A"})");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Extracts correlation ID from events.
    /// </summary>
    public IEnumerable<string> ExtractCorrelationIds(IEnumerable<StreamingEvent> events)
    {
        return events
            .Where(e => !string.IsNullOrEmpty(e.CorrelationId))
            .Select(e => e.CorrelationId!)
            .Distinct();
    }

    /// <summary>
    /// Validates event sequence completeness.
    /// </summary>
    public EventSequenceValidation ValidateSequence(IEnumerable<StreamingEvent> events)
    {
        var eventList = events.ToList();
        var validation = new EventSequenceValidation { IsValid = true, Issues = new List<string>() };

        // Check for required event types
        var eventTypes = eventList.Select(e => e.Type).ToHashSet();
        
        // For a complete workflow, we expect at least classification and gating
        if (!eventTypes.Contains(StreamingEventType.IntentClassified))
            validation.Issues.Add("Missing intent.classified event");

        if (!eventTypes.Contains(StreamingEventType.GateSelected))
            validation.Issues.Add("Missing gate.selected event");

        // Check for sequence number consistency
        var sequenceNumbers = eventList
            .Where(e => e.SequenceNumber.HasValue)
            .Select(e => e.SequenceNumber!.Value)
            .OrderBy(x => x)
            .ToList();

        if (sequenceNumbers.Count > 0)
        {
            for (int i = 1; i < sequenceNumbers.Count; i++)
            {
                if (sequenceNumbers[i] != sequenceNumbers[i - 1] + 1)
                {
                    validation.Issues.Add($"Sequence gap detected between {sequenceNumbers[i - 1]} and {sequenceNumbers[i]}");
                }
            }
        }

        // Check for correlation ID consistency
        var correlationIds = eventList
            .Where(e => !string.IsNullOrEmpty(e.CorrelationId))
            .Select(e => e.CorrelationId!)
            .Distinct()
            .ToList();

        if (correlationIds.Count > 1)
            validation.Issues.Add($"Multiple correlation IDs detected: {string.Join(", ", correlationIds)}");

        validation.IsValid = validation.Issues.Count == 0;
        return validation;
    }

    /// <summary>
    /// Exports events to JSON format.
    /// </summary>
    public string ExportToJson(IEnumerable<StreamingEvent> events)
    {
        var eventList = events.ToList();
        return JsonSerializer.Serialize(eventList, _jsonOptions);
    }

    /// <summary>
    /// Exports events to CSV format.
    /// </summary>
    public string ExportToCsv(IEnumerable<StreamingEvent> events)
    {
        var lines = new List<string>
        {
            "ID,Type,Timestamp,CorrelationId,SequenceNumber,PayloadType"
        };

        foreach (var @event in events)
        {
            var payloadType = @event.Payload?.GetType().Name ?? "null";
            lines.Add($"\"{@event.Id}\",\"{GetEventTypeName(@event.Type)}\",\"{@event.Timestamp:O}\",\"{@event.CorrelationId ?? ""}\",\"{@event.SequenceNumber?.ToString() ?? ""}\",\"{payloadType}\"");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetEventTypeName(StreamingEventType type)
    {
        return type switch
        {
            StreamingEventType.CommandSuggested => "command.suggested",
            StreamingEventType.SuggestedCommandConfirmed => "command.confirmed",
            StreamingEventType.SuggestedCommandRejected => "command.rejected",
            StreamingEventType.IntentClassified => "intent.classified",
            StreamingEventType.GateSelected => "gate.selected",
            StreamingEventType.ToolCall => "tool.call",
            StreamingEventType.ToolResult => "tool.result",
            StreamingEventType.ToolCallDetected => "tool.call.detected",
            StreamingEventType.ToolCallStarted => "tool.call.started",
            StreamingEventType.ToolCallCompleted => "tool.call.completed",
            StreamingEventType.ToolCallFailed => "tool.call.failed",
            StreamingEventType.ToolResultsSubmitted => "tool.results.submitted",
            StreamingEventType.ToolLoopIterationCompleted => "tool.loop.iteration.completed",
            StreamingEventType.ToolLoopCompleted => "tool.loop.completed",
            StreamingEventType.ToolLoopFailed => "tool.loop.failed",
            StreamingEventType.PhaseLifecycle => "phase.lifecycle",
            StreamingEventType.AssistantDelta => "assistant.delta",
            StreamingEventType.AssistantFinal => "assistant.final",
            StreamingEventType.RunLifecycle => "run.lifecycle",
            StreamingEventType.Error => "error",
            StreamingEventType.UiNavigation => "ui.navigation",
            _ => "unknown"
        };
    }
}

/// <summary>
/// Analysis results for an event sequence.
/// </summary>
public class EventSequenceAnalysis
{
    /// <summary>
    /// Total number of events.
    /// </summary>
    public int TotalEvents { get; set; }

    /// <summary>
    /// Count of events by type.
    /// </summary>
    public Dictionary<string, int> EventsByType { get; set; } = new();

    /// <summary>
    /// Sequence numbers where gaps were detected.
    /// </summary>
    public List<long> SequenceGaps { get; set; } = new();

    /// <summary>
    /// Unique correlation IDs in the sequence.
    /// </summary>
    public HashSet<string> CorrelationIds { get; set; } = new();
}

/// <summary>
/// Validation results for an event sequence.
/// </summary>
public class EventSequenceValidation
{
    /// <summary>
    /// Whether the sequence is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Issues found during validation.
    /// </summary>
    public List<string> Issues { get; set; } = new();
}

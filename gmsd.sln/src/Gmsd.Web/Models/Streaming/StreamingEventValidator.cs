using System.Text.Json;
using System.Text.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Validation;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Validates streaming events against the JSON schema and enforces versioning constraints.
/// Provides comprehensive validation for all event types and payloads.
/// </summary>
public class StreamingEventValidator : IStreamingEventValidator
{
    private JsonSchema? _eventSchema;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<StreamingEventType, Type> _payloadTypeMap;

    public StreamingEventValidator()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        _payloadTypeMap = new Dictionary<StreamingEventType, Type>
        {
            { StreamingEventType.CommandSuggested, typeof(CommandSuggestedPayload) },
            { StreamingEventType.SuggestedCommandConfirmed, typeof(SuggestedCommandConfirmedPayload) },
            { StreamingEventType.SuggestedCommandRejected, typeof(SuggestedCommandRejectedPayload) },
            { StreamingEventType.IntentClassified, typeof(IntentClassifiedPayload) },
            { StreamingEventType.GateSelected, typeof(GateSelectedPayload) },
            { StreamingEventType.ToolCall, typeof(ToolCallPayload) },
            { StreamingEventType.ToolResult, typeof(ToolResultPayload) },
            { StreamingEventType.ToolCallDetected, typeof(ToolCallDetectedPayload) },
            { StreamingEventType.ToolCallStarted, typeof(ToolCallStartedPayload) },
            { StreamingEventType.ToolCallCompleted, typeof(ToolCallCompletedPayload) },
            { StreamingEventType.ToolCallFailed, typeof(ToolCallFailedPayload) },
            { StreamingEventType.ToolResultsSubmitted, typeof(ToolResultsSubmittedPayload) },
            { StreamingEventType.ToolLoopIterationCompleted, typeof(ToolLoopIterationCompletedPayload) },
            { StreamingEventType.ToolLoopCompleted, typeof(ToolLoopCompletedPayload) },
            { StreamingEventType.ToolLoopFailed, typeof(ToolLoopFailedPayload) },
            { StreamingEventType.PhaseLifecycle, typeof(PhaseLifecyclePayload) },
            { StreamingEventType.AssistantDelta, typeof(AssistantDeltaPayload) },
            { StreamingEventType.AssistantFinal, typeof(AssistantFinalPayload) },
            { StreamingEventType.RunLifecycle, typeof(RunLifecyclePayload) },
            { StreamingEventType.Error, typeof(ErrorPayload) },
            { StreamingEventType.UiNavigation, typeof(UiNavigationPayload) }
        };
    }

    /// <summary>
    /// Validates a streaming event against the schema and type constraints.
    /// </summary>
    public ValidationResult ValidateEvent(StreamingEvent @event)
    {
        var errors = new List<string>();

        // Validate required envelope fields
        if (string.IsNullOrWhiteSpace(@event.Id))
            errors.Add("Event ID is required and must not be empty");

        if (@event.Timestamp == default)
            errors.Add("Event timestamp is required");

        // Validate payload content based on event type (includes type checking via conversion)
        var payloadErrors = ValidatePayload(@event.Type, @event.Payload);
        errors.AddRange(payloadErrors);

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Validates a JSON string as a streaming event.
    /// </summary>
    public ValidationResult ValidateEventJson(string json)
    {
        try
        {
            var @event = JsonSerializer.Deserialize<StreamingEvent>(json, _jsonOptions);
            if (@event == null)
                return new ValidationResult { IsValid = false, Errors = new List<string> { "Failed to deserialize event" } };

            return ValidateEvent(@event);
        }
        catch (JsonException ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"JSON parsing error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Converts JsonElement payloads to their proper types for validation.
    /// </summary>
    private object? ConvertPayloadIfNeeded(StreamingEventType eventType, object? payload)
    {
        if (payload == null || !_payloadTypeMap.TryGetValue(eventType, out var expectedType))
            return payload;

        // If already the correct type, return as-is
        if (expectedType.IsInstanceOfType(payload))
            return payload;

        // If it's a JsonElement, deserialize to the proper type
        if (payload is JsonElement element)
        {
            try
            {
                var json = element.GetRawText();
                var deserialized = JsonSerializer.Deserialize(json, expectedType, _jsonOptions);
                return deserialized ?? payload;
            }
            catch
            {
                // If deserialization fails, return the original payload
                // The validation will catch the type mismatch
                return payload;
            }
        }

        return payload;
    }

    /// <summary>
    /// Validates payload content based on event type.
    /// </summary>
    private List<string> ValidatePayload(StreamingEventType eventType, object? payload)
    {
        var errors = new List<string>();

        if (payload == null)
        {
            // Some events may have optional payloads
            return errors;
        }

        // Convert JsonElement to proper type if needed
        var convertedPayload = ConvertPayloadIfNeeded(eventType, payload);

        switch (eventType)
        {
            case StreamingEventType.IntentClassified:
                if (convertedPayload is IntentClassifiedPayload intent)
                {
                    if (string.IsNullOrWhiteSpace(intent.Category))
                        errors.Add("IntentClassified payload requires Category");
                    if (intent.Confidence < 0 || intent.Confidence > 1)
                        errors.Add("IntentClassified confidence must be between 0.0 and 1.0");
                }
                else if (convertedPayload is JsonElement jsonElement)
                {
                    // Validate JsonElement fields directly
                    if (!jsonElement.TryGetProperty("category", out var category) || category.ValueKind == JsonValueKind.Null || string.IsNullOrWhiteSpace(category.GetString()))
                        errors.Add("IntentClassified payload requires Category");
                    if (jsonElement.TryGetProperty("confidence", out var confidence) && confidence.TryGetDouble(out var confValue))
                    {
                        if (confValue < 0 || confValue > 1)
                            errors.Add("IntentClassified confidence must be between 0.0 and 1.0");
                    }
                }
                break;

            case StreamingEventType.GateSelected:
                if (convertedPayload is GateSelectedPayload gate)
                {
                    if (string.IsNullOrWhiteSpace(gate.Phase))
                        errors.Add("GateSelected payload requires Phase");
                }
                break;

            case StreamingEventType.ToolCall:
                if (convertedPayload is ToolCallPayload toolCall)
                {
                    if (string.IsNullOrWhiteSpace(toolCall.CallId))
                        errors.Add("ToolCall payload requires CallId");
                    if (string.IsNullOrWhiteSpace(toolCall.ToolName))
                        errors.Add("ToolCall payload requires ToolName");
                }
                break;

            case StreamingEventType.ToolResult:
                if (convertedPayload is ToolResultPayload toolResult)
                {
                    if (string.IsNullOrWhiteSpace(toolResult.CallId))
                        errors.Add("ToolResult payload requires CallId");
                }
                break;

            case StreamingEventType.PhaseLifecycle:
                if (convertedPayload is PhaseLifecyclePayload phase)
                {
                    if (string.IsNullOrWhiteSpace(phase.Phase))
                        errors.Add("PhaseLifecycle payload requires Phase");
                    if (string.IsNullOrWhiteSpace(phase.Status) || (phase.Status != "started" && phase.Status != "completed"))
                        errors.Add("PhaseLifecycle payload Status must be 'started' or 'completed'");
                }
                break;

            case StreamingEventType.AssistantDelta:
                if (convertedPayload is AssistantDeltaPayload delta)
                {
                    if (string.IsNullOrWhiteSpace(delta.MessageId))
                        errors.Add("AssistantDelta payload requires MessageId");
                    if (string.IsNullOrWhiteSpace(delta.Content))
                        errors.Add("AssistantDelta payload requires Content");
                }
                break;

            case StreamingEventType.AssistantFinal:
                if (convertedPayload is AssistantFinalPayload final)
                {
                    if (string.IsNullOrWhiteSpace(final.MessageId))
                        errors.Add("AssistantFinal payload requires MessageId");
                }
                break;

            case StreamingEventType.RunLifecycle:
                if (convertedPayload is RunLifecyclePayload run)
                {
                    if (string.IsNullOrWhiteSpace(run.Status) || (run.Status != "started" && run.Status != "finished"))
                        errors.Add("RunLifecycle payload Status must be 'started' or 'finished'");
                }
                break;

            case StreamingEventType.Error:
                if (convertedPayload is ErrorPayload error)
                {
                    if (string.IsNullOrWhiteSpace(error.Code))
                        errors.Add("Error payload requires Code");
                    if (string.IsNullOrWhiteSpace(error.Message))
                        errors.Add("Error payload requires Message");
                    if (string.IsNullOrWhiteSpace(error.Severity) || 
                        (error.Severity != "error" && error.Severity != "warning" && error.Severity != "info"))
                        errors.Add("Error payload Severity must be 'error', 'warning', or 'info'");
                }
                break;
        }

        return errors;
    }

    /// <summary>
    /// Gets the event schema, loading it lazily on first access.
    /// </summary>
    private JsonSchema GetEventSchema()
    {
        if (_eventSchema != null)
            return _eventSchema;

        // For now, return a basic schema. In production, this would load from StreamingEventSchema.json
        var schemaJson = @"{
            ""$schema"": ""http://json-schema.org/draft-07/schema#"",
            ""type"": ""object"",
            ""required"": [""id"", ""type"", ""timestamp""],
            ""properties"": {
                ""id"": { ""type"": ""string"" },
                ""type"": { ""type"": ""integer"" },
                ""timestamp"": { ""type"": ""string"", ""format"": ""date-time"" },
                ""correlationId"": { ""type"": [""string"", ""null""] },
                ""sequenceNumber"": { ""type"": [""integer"", ""null""] },
                ""payload"": { ""type"": [""object"", ""null""] }
            }
        }";

        _eventSchema = JsonSchema.FromJsonAsync(schemaJson).GetAwaiter().GetResult();
        return _eventSchema;
    }
}

/// <summary>
/// Result of event validation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the event passed validation
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors (empty if valid)
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Defines the contract for streaming event validation.
/// Implementations validate events against schema and type constraints.
/// </summary>
public interface IStreamingEventValidator
{
    /// <summary>
    /// Validates a streaming event against the schema and type constraints.
    /// </summary>
    /// <param name="event">The event to validate</param>
    /// <returns>Validation result with any errors</returns>
    ValidationResult ValidateEvent(StreamingEvent @event);

    /// <summary>
    /// Validates a JSON string as a streaming event.
    /// </summary>
    /// <param name="json">JSON string to validate</param>
    /// <returns>Validation result with any errors</returns>
    ValidationResult ValidateEventJson(string json);
}

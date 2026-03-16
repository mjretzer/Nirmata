namespace nirmata.Aos.Public;

using System.Text.Json;
using nirmata.Aos.Contracts.State;

/// <summary>
/// Event store for append-only event log operations.
/// </summary>
/// <remarks>
/// The event store provides a service abstraction over event log operations
/// defined by the aos-state-store spec. It handles:
/// - Appending events to the event log
/// - Tailing the most recent events
/// - Listing events with filters
/// </remarks>
public interface IEventStore
{
    /// <summary>
    /// Appends a JSON object event to <c>.aos/state/events.ndjson</c> (NDJSON).
    /// </summary>
    /// <param name="payload">The JSON payload to append as an event.</param>
    /// <exception cref="ArgumentException">Thrown when payload is not a valid JSON object.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the workspace is not initialized or write fails.</exception>
    /// <remarks>
    /// Events are appended atomically to ensure durability.
    /// The file uses LF line endings as per the NDJSON format.
    /// </remarks>
    void AppendEvent(JsonElement payload);

    /// <summary>
    /// Tails the last n events from the event log.
    /// </summary>
    /// <param name="n">The number of events to return.</param>
    /// <returns>A read-only list of event entries in file order (oldest to newest), capped at n items.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when n is negative.</exception>
    /// <remarks>
    /// Returns events in the same order they appear in the file.
    /// If n exceeds the total event count, returns all events.
    /// </remarks>
    IReadOnlyList<StateEventEntry> Tail(int n);

    /// <summary>
    /// Lists events with optional filters.
    /// </summary>
    /// <param name="request">The tail/filter request options.</param>
    /// <returns>A response containing the filtered events in file order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the event log is corrupted.</exception>
    /// <remarks>
    /// Supports filtering by:
    /// - <see cref="StateEventTailRequest.SinceLine"/>: skip events at or before this line (exclusive)
    /// - <see cref="StateEventTailRequest.MaxItems"/>: cap the number of returned events
    /// - <see cref="StateEventTailRequest.EventType"/>: filter by event type
    /// - <see cref="StateEventTailRequest.Kind"/>: filter by legacy kind
    /// Multiple filters are combined with AND logic.
    /// </remarks>
    StateEventTailResponse ListEvents(StateEventTailRequest request);
}

using System.Text.Json;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Engine.Stores;

namespace nirmata.Aos.Public;

/// <summary>
/// Public event store implementation for append-only event log operations.
/// </summary>
public sealed class EventStore : IEventStore
{
    private readonly AosStateStore _inner;

    private EventStore(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _inner = new AosStateStore(aosRootPath);
    }

    /// <summary>
    /// Creates an event store for an explicit <c>.aos</c> root path.
    /// </summary>
    public static EventStore FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates an event store for a workspace's <c>.aos</c> root.
    /// </summary>
    public static EventStore FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new EventStore(workspace.AosRootPath);
    }

    /// <inheritdoc />
    public void AppendEvent(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Event payload must be a JSON object.", nameof(payload));
        }

        _inner.AppendEvent(payload);
    }

    /// <inheritdoc />
    public IReadOnlyList<StateEventEntry> Tail(int n)
    {
        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n), "n must be non-negative.");

        // Read all events, then take the last n while preserving file order
        var response = _inner.TailEvents(new StateEventTailRequest { SinceLine = 0 });
        var allEvents = response.Items;

        if (n >= allEvents.Count)
        {
            return allEvents;
        }

        // Return last n events in file order (oldest to newest)
        return allEvents.Skip(allEvents.Count - n).ToList();
    }

    /// <inheritdoc />
    public StateEventTailResponse ListEvents(StateEventTailRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return _inner.TailEvents(request);
    }
}

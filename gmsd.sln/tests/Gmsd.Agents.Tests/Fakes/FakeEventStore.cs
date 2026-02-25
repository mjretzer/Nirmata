using System.Text.Json;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IEventStore for unit testing.
/// </summary>
public sealed class FakeEventStore : IEventStore
{
    private readonly List<JsonElement> _events = new();

    /// <inheritdoc />
    public void AppendEvent(JsonElement payload)
    {
        // Clone the element to ensure we capture the data before it's disposed
        _events.Add(payload.Clone());
    }

    /// <inheritdoc />
    public IReadOnlyList<StateEventEntry> Tail(int n)
    {
        return _events
            .Skip(Math.Max(0, _events.Count - n))
            .Select((e, i) => new StateEventEntry
            {
                LineNumber = i + 1,
                Payload = e
            })
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public StateEventTailResponse ListEvents(StateEventTailRequest request)
    {
        var filtered = _events.AsEnumerable();

        if (!string.IsNullOrEmpty(request.EventType))
        {
            filtered = filtered.Where(e =>
                e.TryGetProperty("eventType", out var et) &&
                et.GetString() == request.EventType);
        }

        var events = filtered
            .Select((e, i) => new StateEventEntry
            {
                LineNumber = i + 1,
                Payload = e
            })
            .ToList();

        return new StateEventTailResponse
        {
            Items = events.AsReadOnly()
        };
    }

    /// <summary>
    /// Gets all recorded events for verification.
    /// </summary>
    public IReadOnlyList<JsonElement> GetRecordedEvents() => _events.AsReadOnly();

    /// <summary>
    /// Gets recorded events filtered by event type.
    /// </summary>
    public IReadOnlyList<JsonElement> GetEventsByType(string eventType)
    {
        return _events
            .Where(e => e.TryGetProperty("eventType", out var type) && type.GetString() == eventType)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Resets the fake, clearing all recorded events.
    /// </summary>
    public void Reset()
    {
        _events.Clear();
    }
}

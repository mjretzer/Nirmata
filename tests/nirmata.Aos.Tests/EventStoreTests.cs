using System.Text.Json;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class EventStoreTests
{
    [Fact]
    public void AppendThenTail_ReturnsExpectedOrder_PerAosStateStoreSpec()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var eventStore = EventStore.FromAosRoot(aosRoot);

            // Append 5 events with distinct sequence identifiers
            for (int i = 1; i <= 5; i++)
            {
                var payload = JsonSerializer.SerializeToElement(new
                {
                    schemaVersion = 1,
                    eventType = "test.event",
                    sequence = i,
                    timestampUtc = $"2026-02-03T00:00:0{i}Z"
                });
                eventStore.AppendEvent(payload);
            }

            // Tail(3) should return the last 3 events in chronological order (oldest to newest)
            var tailResult = eventStore.Tail(3);

            Assert.Equal(3, tailResult.Count);
            Assert.Equal(3, tailResult[0].Payload.GetProperty("sequence").GetInt32());
            Assert.Equal(4, tailResult[1].Payload.GetProperty("sequence").GetInt32());
            Assert.Equal(5, tailResult[2].Payload.GetProperty("sequence").GetInt32());

            // Verify line numbers are in ascending order (file order)
            Assert.True(tailResult[0].LineNumber < tailResult[1].LineNumber);
            Assert.True(tailResult[1].LineNumber < tailResult[2].LineNumber);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void Tail_WithNLargerThanCount_ReturnsAllEvents()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var eventStore = EventStore.FromAosRoot(aosRoot);

            // Append 3 events
            for (int i = 1; i <= 3; i++)
            {
                var payload = JsonSerializer.SerializeToElement(new
                {
                    schemaVersion = 1,
                    eventType = "test.event",
                    sequence = i
                });
                eventStore.AppendEvent(payload);
            }

            // Tail(10) should return all 3 events
            var tailResult = eventStore.Tail(10);

            Assert.Equal(3, tailResult.Count);
            Assert.Equal(1, tailResult[0].Payload.GetProperty("sequence").GetInt32());
            Assert.Equal(2, tailResult[1].Payload.GetProperty("sequence").GetInt32());
            Assert.Equal(3, tailResult[2].Payload.GetProperty("sequence").GetInt32());
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void Tail_WithZero_ReturnsEmptyList()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var eventStore = EventStore.FromAosRoot(aosRoot);

            // Append an event
            var payload = JsonSerializer.SerializeToElement(new
            {
                schemaVersion = 1,
                eventType = "test.event"
            });
            eventStore.AppendEvent(payload);

            // Tail(0) should return empty list
            var tailResult = eventStore.Tail(0);

            Assert.Empty(tailResult);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void AppendEvent_NonObjectPayload_ThrowsArgumentException()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var eventStore = EventStore.FromAosRoot(aosRoot);

            // Array payload should throw
            var arrayPayload = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 });
            Assert.Throws<ArgumentException>(() => eventStore.AppendEvent(arrayPayload));

            // String payload should throw
            var stringPayload = JsonSerializer.SerializeToElement("not an object");
            Assert.Throws<ArgumentException>(() => eventStore.AppendEvent(stringPayload));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void Tail_NegativeN_ThrowsArgumentOutOfRangeException()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var eventStore = EventStore.FromAosRoot(aosRoot);

            Assert.Throws<ArgumentOutOfRangeException>(() => eventStore.Tail(-1));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ListEvents_WithFilters_ReturnsFilteredResults()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var eventStore = EventStore.FromAosRoot(aosRoot);

            // Append events of different types
            eventStore.AppendEvent(JsonSerializer.SerializeToElement(new
            {
                schemaVersion = 1,
                eventType = "cursor.set",
                detail = "first"
            }));

            eventStore.AppendEvent(JsonSerializer.SerializeToElement(new
            {
                schemaVersion = 1,
                eventType = "checkpoint.created",
                detail = "second"
            }));

            eventStore.AppendEvent(JsonSerializer.SerializeToElement(new
            {
                schemaVersion = 1,
                eventType = "cursor.set",
                detail = "third"
            }));

            // Filter by eventType
            var filtered = eventStore.ListEvents(new StateEventTailRequest
            {
                SinceLine = 0,
                EventType = "cursor.set"
            });

            Assert.Equal(2, filtered.Items.Count);
            Assert.Equal("first", filtered.Items[0].Payload.GetProperty("detail").GetString());
            Assert.Equal("third", filtered.Items[1].Payload.GetProperty("detail").GetString());
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-event-store", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

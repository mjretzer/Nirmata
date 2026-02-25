using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Gmsd.Web.Models.Streaming;
using Xunit;

namespace Gmsd.Web.Tests.Models.Streaming;

/// <summary>
/// Performance benchmarks for the streaming event system.
/// Validates latency targets: < 50ms event emission, < 50ms serialization per event.
/// 
/// Note: UI render latency tests (< 50ms per event) are located in:
/// tests/Gmsd.Web/js/streaming-performance.test.js (JavaScript-side renderer tests)
/// </summary>
public class StreamingPerformanceTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region T22-1: Event Emission Latency (< 50ms target)

    [Fact]
    public async Task EventEmission_SingleEvent_LatencyUnder50ms()
    {
        // Arrange
        var sink = new ChannelEventSink();
        var payload = new IntentClassifiedPayload
        {
            Category = "Chat",
            Confidence = 0.95,
            Reasoning = "Test reasoning",
            UserInput = "hello"
        };
        var @event = StreamingEvent.Create(StreamingEventType.IntentClassified, payload);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await sink.EmitAsync(@event);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
            "Event emission latency should be under 50ms");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task EventEmission_MultipleEvents_AverageLatencyUnder50ms(int eventCount)
    {
        // Arrange
        var sink = new ChannelEventSink();
        var events = Enumerable.Range(0, eventCount).Select(i =>
            StreamingEvent.Create(
                StreamingEventType.AssistantDelta,
                new AssistantDeltaPayload
                {
                    MessageId = $"msg-{i}",
                    Content = $"Chunk {i}",
                    Index = i
                }
            )
        ).ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        foreach (var evt in events)
        {
            await sink.EmitAsync(evt);
        }
        stopwatch.Stop();

        var avgLatency = stopwatch.ElapsedMilliseconds / (double)eventCount;

        // Assert
        avgLatency.Should().BeLessThan(50,
            "Average event emission latency should be under 50ms");
    }

    [Fact]
    public async Task EventEmission_BurstOf100Events_LatencyUnder50msEach()
    {
        // Arrange
        var sink = new ChannelEventSink();
        const int eventCount = 100;
        var latencies = new List<long>();

        // Act
        for (int i = 0; i < eventCount; i++)
        {
            var @event = StreamingEvent.Create(
                StreamingEventType.AssistantDelta,
                new AssistantDeltaPayload
                {
                    MessageId = "burst-test",
                    Content = $"Chunk {i}",
                    Index = i
                }
            );

            var sw = Stopwatch.StartNew();
            await sink.EmitAsync(@event);
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Assert
        var maxLatency = latencies.Max();
        var avgLatency = latencies.Average();
        var p95Latency = latencies.OrderBy(l => l).Skip((int)(eventCount * 0.95)).First();

        maxLatency.Should().BeLessThan(100, "Max latency should be under 100ms even during bursts");
        avgLatency.Should().BeLessThan(50, "Average latency should be under 50ms");
        p95Latency.Should().BeLessThan(75, "95th percentile should be under 75ms");
    }

    #endregion

    #region T22-2: JSON Serialization Latency (< 50ms per event target)
    // Note: UI rendering tests (< 50ms) are in JavaScript test suite:
    // tests/Gmsd.Web/js/streaming-performance.test.js

    [Fact]
    public void Serialization_IntentClassified_Under50ms()
    {
        // Arrange
        var @event = StreamingEvent.Create(
            StreamingEventType.IntentClassified,
            new IntentClassifiedPayload
            {
                Category = "Write",
                Confidence = 0.87,
                Reasoning = "User wants to create a new file with specific requirements",
                UserInput = "create a file"
            }
        );

        // Act
        var stopwatch = Stopwatch.StartNew();
        var json = JsonSerializer.Serialize(@event, _jsonOptions);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
            "IntentClassified serialization should be under 50ms");
        json.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Serialization_GateSelected_Under50ms()
    {
        // Arrange
        var @event = StreamingEvent.Create(
            StreamingEventType.GateSelected,
            new GateSelectedPayload
            {
                Phase = "Planner",
                Reasoning = "User input requires planning phase",
                RequiresConfirmation = false
            }
        );

        // Act
        var stopwatch = Stopwatch.StartNew();
        var json = JsonSerializer.Serialize(@event, _jsonOptions);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
            "GateSelected serialization should be under 50ms");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void Serialization_MultipleEvents_AverageUnder50ms(int eventCount)
    {
        // Arrange
        var events = Enumerable.Range(0, eventCount).Select(i =>
            StreamingEvent.Create(
                StreamingEventType.AssistantDelta,
                new AssistantDeltaPayload
                {
                    MessageId = "perf-test",
                    Content = $"Token {i} ",
                    Index = i
                }
            )
        ).ToList();

        var serializeTimes = new List<long>();

        // Act
        foreach (var @event in events)
        {
            var sw = Stopwatch.StartNew();
            JsonSerializer.Serialize(@event, _jsonOptions);
            sw.Stop();
            serializeTimes.Add(sw.ElapsedMilliseconds);
        }

        // Assert
        var avgTime = serializeTimes.Average();
        var maxTime = serializeTimes.Max();

        avgTime.Should().BeLessThan(50,
            $"Average serialization time for {eventCount} events should be under 50ms");
        maxTime.Should().BeLessThan(100,
            "Max serialization time should be under 100ms");
    }

    #endregion

    #region T22-3: Test with 100+ Event Sequences

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(500)]
    public async Task EventSequence_LargeSequence_EmitsSuccessfully(int eventCount)
    {
        // Arrange
        var sink = new ChannelEventSink();
        var events = GenerateEventSequence(eventCount);

        // Act
        var stopwatch = Stopwatch.StartNew();
        foreach (var @event in events)
        {
            await sink.EmitAsync(@event);
        }
        stopwatch.Stop();

        // Assert
        var emittedCount = 0;
        while (await sink.Reader.WaitToReadAsync())
        {
            if (sink.Reader.TryRead(out _))
            {
                emittedCount++;
            }
            if (emittedCount >= eventCount) break;
        }

        emittedCount.Should().Be(eventCount);
        
        // Total time should be reasonable (under 5 seconds for any sequence size)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            $"Emitting {eventCount} events should complete in under 5 seconds");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(500)]
    public void EventSerialization_LargeSequence_RoundTripsSuccessfully(int eventCount)
    {
        // Arrange
        var events = GenerateEventSequence(eventCount);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var serialized = events.Select(e => JsonSerializer.Serialize(e, _jsonOptions)).ToList();
        var deserialized = serialized.Select(s => JsonSerializer.Deserialize<StreamingEvent>(s, _jsonOptions)).ToList();
        stopwatch.Stop();

        // Assert
        deserialized.Count.Should().Be(eventCount);
        deserialized.Should().AllSatisfy(d => d.Should().NotBeNull());
        
        // Serialization should be fast (under 1 second for 500 events)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000,
            $"Serializing {eventCount} events should take under 1 second");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void EventSequence_OrderingValidation_SequencesAreCorrect(int eventCount)
    {
        // Arrange
        var events = GenerateEventSequence(eventCount);

        // Act - Verify sequence numbers are in order
        var eventsWithSequences = events
            .Where(e => e.SequenceNumber.HasValue)
            .OrderBy(e => e.SequenceNumber!.Value)
            .ToList();

        // Assert
        eventsWithSequences.Count.Should().BeGreaterThan(0);
        
        for (int i = 1; i < eventsWithSequences.Count; i++)
        {
            var prevSeq = eventsWithSequences[i - 1].SequenceNumber!.Value;
            var currSeq = eventsWithSequences[i].SequenceNumber!.Value;
            currSeq.Should().BeGreaterThan(prevSeq, 
                "Events should have sequential sequence numbers");
        }
    }

    #endregion

    #region T22-4: Memory Usage Profiling

    [Fact]
    public void Memory_LargeEventSequence_NoSignificantGrowth()
    {
        // Arrange
        const int eventCount = 1000;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(true);

        // Act
        var events = new List<StreamingEvent>();
        for (int i = 0; i < eventCount; i++)
        {
            events.Add(StreamingEvent.Create(
                StreamingEventType.AssistantDelta,
                new AssistantDeltaPayload
                {
                    MessageId = "memory-test",
                    Content = new string('x', 100), // 100 char content
                    Index = i
                },
                correlationId: "test-correlation",
                sequenceNumber: i
            ));
        }

        var afterCreation = GC.GetTotalMemory(true);
        
        // Clear references
        events.Clear();
        events = null;
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var afterCleanup = GC.GetTotalMemory(true);

        // Assert
        var memoryGrowth = afterCreation - initialMemory;
        var memoryPerEvent = memoryGrowth / (double)eventCount;
        
        // Should use less than 10KB per event on average
        memoryPerEvent.Should().BeLessThan(10 * 1024,
            $"Memory per event should be under 10KB (actual: {memoryPerEvent / 1024:F2}KB)");

        // Memory should be reclaimable
        var memoryReclaimed = afterCreation - afterCleanup;
        var reclaimRatio = memoryGrowth > 0 ? memoryReclaimed / (double)memoryGrowth : 1;
        reclaimRatio.Should().BeGreaterThan(0.7,
            "At least 70% of memory should be reclaimable after cleanup");
    }

    [Fact]
    public async Task Memory_ChannelSink_BufferedEvents_LimitedGrowth()
    {
        // Arrange
        const int eventCount = 200;
        var sink = new ChannelEventSink();

        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Emit events without reading (buffered)
        for (int i = 1; i <= eventCount; i++)
        {
            await sink.EmitAsync(StreamingEvent.Create(
                StreamingEventType.AssistantDelta,
                new AssistantDeltaPayload
                {
                    MessageId = "buffer-test",
                    Content = $"Event {i}",
                    Index = i
                },
                sequenceNumber: i
            ));
        }

        var afterBuffering = GC.GetTotalMemory(true);

        // Assert
        var memoryGrowth = afterBuffering - initialMemory;
        var memoryPerEvent = memoryGrowth / (double)eventCount;
        
        // Each event should use less than 5KB when buffered
        memoryPerEvent.Should().BeLessThan(5 * 1024,
            $"Buffered memory per event should be under 5KB (actual: {memoryPerEvent / 1024:F2}KB)");
    }

    [Fact]
    public void Memory_Serialization_TemporaryAllocations_Minimal()
    {
        // Arrange
        const int iterations = 100;
        var @event = StreamingEvent.Create(
            StreamingEventType.AssistantFinal,
            new AssistantFinalPayload
            {
                MessageId = "memory-test",
                Content = new string('x', 1000), // 1KB content
                ContentType = "text/plain"
            }
        );

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Serialize same event multiple times
        for (int i = 0; i < iterations; i++)
        {
            var json = JsonSerializer.Serialize(@event, _jsonOptions);
            // Don't hold reference
            _ = json.Length;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var afterSerialization = GC.GetTotalMemory(true);

        // Assert - Memory growth should be minimal after GC
        var memoryGrowth = afterSerialization - initialMemory;
        
        // Should have less than 50KB growth after all the serialization work
        memoryGrowth.Should().BeLessThan(50 * 1024,
            $"Memory growth after {iterations} serializations should be under 50KB (actual: {memoryGrowth / 1024:F2}KB)");
    }

    #endregion

    #region Helper Methods

    private List<StreamingEvent> GenerateEventSequence(int count)
    {
        var events = new List<StreamingEvent>();
        var correlationId = Guid.NewGuid().ToString("N");

        // Start with intent classified
        events.Add(StreamingEvent.Create(
            StreamingEventType.IntentClassified,
            new IntentClassifiedPayload
            {
                Category = "Write",
                Confidence = 0.95,
                Reasoning = "User wants to perform a write operation"
            },
            correlationId: correlationId,
            sequenceNumber: 1
        ));

        // Gate selected
        events.Add(StreamingEvent.Create(
            StreamingEventType.GateSelected,
            new GateSelectedPayload
            {
                Phase = "Planner",
                Reasoning = "Planning phase selected"
            },
            correlationId: correlationId,
            sequenceNumber: 2
        ));

        // Run started
        events.Add(StreamingEvent.Create(
            StreamingEventType.RunLifecycle,
            new RunLifecyclePayload { Status = "started", RunId = "run-123" },
            correlationId: correlationId,
            sequenceNumber: 3
        ));

        // Phase lifecycle events
        var phases = new[] { "Planner", "Executor", "Verifier" };
        int seq = 4;
        foreach (var phase in phases)
        {
            events.Add(StreamingEvent.Create(
                StreamingEventType.PhaseLifecycle,
                new PhaseLifecyclePayload { Phase = phase, Status = "started" },
                correlationId: correlationId,
                sequenceNumber: seq++
            ));

            // Some assistant deltas in each phase
            for (int i = 0; i < 5; i++)
            {
                events.Add(StreamingEvent.Create(
                    StreamingEventType.AssistantDelta,
                    new AssistantDeltaPayload
                    {
                        MessageId = $"msg-{phase}",
                        Content = $"Progress in {phase}... ",
                        Index = i
                    },
                    correlationId: correlationId,
                    sequenceNumber: seq++
                ));
            }

            events.Add(StreamingEvent.Create(
                StreamingEventType.PhaseLifecycle,
                new PhaseLifecyclePayload { Phase = phase, Status = "completed" },
                correlationId: correlationId,
                sequenceNumber: seq++
            ));
        }

        // Assistant final
        events.Add(StreamingEvent.Create(
            StreamingEventType.AssistantFinal,
            new AssistantFinalPayload
            {
                MessageId = "final",
                Content = "Operation completed successfully",
                ContentType = "text/plain"
            },
            correlationId: correlationId,
            sequenceNumber: seq++
        ));

        // Run finished
        events.Add(StreamingEvent.Create(
            StreamingEventType.RunLifecycle,
            new RunLifecyclePayload
            {
                Status = "finished",
                RunId = "run-123",
                Success = true,
                DurationMs = 5000
            },
            correlationId: correlationId,
            sequenceNumber: seq++
        ));

        // Fill remaining with more delta events if needed
        while (events.Count < count)
        {
            events.Add(StreamingEvent.Create(
                StreamingEventType.AssistantDelta,
                new AssistantDeltaPayload
                {
                    MessageId = "filler",
                    Content = $"Filler event {events.Count}",
                    Index = events.Count
                },
                correlationId: correlationId,
                sequenceNumber: seq++
            ));
        }

        return events.Take(count).ToList();
    }

    #endregion
}

using System.Text;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Engine.State;
using Gmsd.Aos.Engine.Stores;
using Gmsd.Aos.Public;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosStateStoreTests
{
    [Fact]
    public void WriteStateSnapshotOverwrite_WritesCanonicalDeterministicJson_AndAvoidsChurn()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var store = new AosStateStore(aosRoot);
            var snapshot = new StateSnapshotDocument(SchemaVersion: 1, Cursor: new StateCursorDocument());

            store.WriteStateSnapshotOverwrite(snapshot);
            var path = Path.Combine(aosRoot, "state", "state.json");

            Assert.True(File.Exists(path), "Expected state snapshot file to exist.");

            var bytes1 = File.ReadAllBytes(path);

            // Guardrail: canonical JSON writer always emits UTF-8 without BOM.
            Assert.False(
                bytes1.Length >= 3 && bytes1[0] == 0xEF && bytes1[1] == 0xBB && bytes1[2] == 0xBF,
                "Expected no UTF-8 BOM."
            );

            // Guardrail: canonical JSON writer always emits a trailing LF.
            Assert.True(bytes1.Length > 0 && bytes1[^1] == (byte)'\n', "Expected trailing LF.");

            // Second write should be a no-churn overwrite (bytes remain identical).
            store.WriteStateSnapshotOverwrite(snapshot);
            var bytes2 = File.ReadAllBytes(path);

            Assert.Equal(bytes1, bytes2);

            // Sanity: contains required fields.
            var text = Encoding.UTF8.GetString(bytes2);
            Assert.Contains("\"schemaVersion\": 1", text, StringComparison.Ordinal);
            Assert.Contains("\"cursor\": {", text, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void DeriveAndWriteStateSnapshotFromEventsOverwrite_SameEventLogYieldsByteIdenticalStateJson()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var store = new AosStateStore(aosRoot);

            store.AppendEvent(new
            {
                schemaVersion = 1,
                eventType = "cursor.set",
                timestampUtc = "2026-02-02T00:00:00Z",
                cursor = new { milestoneId = "milestone:1", milestoneStatus = "in_progress" }
            });

            store.AppendEvent(new
            {
                schemaVersion = 1,
                eventType = "cursor.set",
                timestampUtc = "2026-02-02T00:00:01Z",
                data = new { cursor = new { phaseId = "phase:2", phaseStatus = "done" } }
            });

            store.DeriveAndWriteStateSnapshotFromEventsOverwrite();
            var path = Path.Combine(aosRoot, "state", "state.json");
            Assert.True(File.Exists(path), "Expected derived state snapshot file to exist.");
            var bytes1 = File.ReadAllBytes(path);

            // Re-derive from the same event log; output must be byte-identical.
            store.DeriveAndWriteStateSnapshotFromEventsOverwrite();
            var bytes2 = File.ReadAllBytes(path);

            Assert.Equal(bytes1, bytes2);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void AppendEvent_AppendsNdjsonWithoutRewritingHistory_AndUsesLfOnly()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var store = new AosStateStore(aosRoot);

            store.AppendEvent(new { schemaVersion = 1, type = "first", detail = "a" });
            var path = Path.Combine(aosRoot, "state", "events.ndjson");

            Assert.True(File.Exists(path), "Expected events log file to exist.");
            var bytes1 = File.ReadAllBytes(path);

            store.AppendEvent(new { schemaVersion = 1, type = "second", detail = "b" });
            var bytes2 = File.ReadAllBytes(path);

            Assert.True(bytes2.Length > bytes1.Length, "Expected file to grow after appending an event.");
            Assert.True(bytes2.AsSpan(0, bytes1.Length).SequenceEqual(bytes1), "Expected previous bytes to remain unchanged.");

            // LF-only requirement (no CR bytes).
            Assert.DoesNotContain((byte)'\r', bytes2);

            var events = store.ReadEvents();
            Assert.Equal(2, events.Count);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void EnsureWorkspaceInitialized_WhenStateAndEventsMissing_BootstrapsDeterministicArtifacts()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var store = StateStore.FromAosRoot(aosRoot);

            store.EnsureWorkspaceInitialized();

            var statePath = Path.Combine(aosRoot, "state", "state.json");
            var eventsPath = Path.Combine(aosRoot, "state", "events.ndjson");

            Assert.True(File.Exists(statePath), "Expected state.json to be created during workspace initialization.");
            Assert.True(File.Exists(eventsPath), "Expected events.ndjson to be created during workspace initialization.");

            var stateBytesFirst = File.ReadAllBytes(statePath);
            var eventsBytesFirst = File.ReadAllBytes(eventsPath);

            // Idempotency: running preflight again with unchanged inputs should not churn bytes.
            store.EnsureWorkspaceInitialized();

            var stateBytesSecond = File.ReadAllBytes(statePath);
            var eventsBytesSecond = File.ReadAllBytes(eventsPath);

            Assert.Equal(stateBytesFirst, stateBytesSecond);
            Assert.Equal(eventsBytesFirst, eventsBytesSecond);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void EnsureWorkspaceInitialized_WhenStateSnapshotIsStale_RebuildsSnapshotFromEvents()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var innerStore = new AosStateStore(aosRoot);

            innerStore.WriteStateSnapshotOverwrite(new StateSnapshotDocument(
                SchemaVersion: 1,
                Cursor: new StateCursorDocument(PhaseId: "phase:stale", PhaseStatus: "done")));

            innerStore.AppendEvent(new
            {
                schemaVersion = 1,
                eventType = "cursor.set",
                cursor = new { phaseId = "phase:fresh", phaseStatus = "in_progress" }
            });

            var store = StateStore.FromAosRoot(aosRoot);

            store.EnsureWorkspaceInitialized();

            var snapshot = store.ReadSnapshot();
            Assert.Equal("phase:fresh", snapshot.Cursor.PhaseId);
            Assert.Equal("in_progress", snapshot.Cursor.PhaseStatus);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void TailEvents_SupportsSinceLineMaxItemsAndFilters_AndPreservesFileOrder()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var store = new AosStateStore(aosRoot);

            store.AppendEvent(new { schemaVersion = 1, eventType = "cursor.set", detail = "a" });
            store.AppendEvent(new { schemaVersion = 1, kind = "checkpoint.created", detail = "b" });
            store.AppendEvent(new { schemaVersion = 1, eventType = "checkpoint.created", detail = "c" });
            store.AppendEvent(new { schemaVersion = 1, eventType = "checkpoint.created", detail = "d" });

            var all = store.TailEvents(new StateEventTailRequest { SinceLine = 0 });
            Assert.Equal(4, all.Items.Count);
            Assert.Equal(1, all.Items[0].LineNumber);
            Assert.Equal(2, all.Items[1].LineNumber);
            Assert.Equal(3, all.Items[2].LineNumber);
            Assert.Equal(4, all.Items[3].LineNumber);

            var eventTypeFiltered = store.TailEvents(new StateEventTailRequest { SinceLine = 0, EventType = "cursor.set" });
            Assert.Single(eventTypeFiltered.Items);
            Assert.Equal(1, eventTypeFiltered.Items[0].LineNumber);

            var kindFiltered = store.TailEvents(new StateEventTailRequest { SinceLine = 0, Kind = "checkpoint.created" });
            Assert.Single(kindFiltered.Items);
            Assert.Equal(2, kindFiltered.Items[0].LineNumber);

            var paged = store.TailEvents(new StateEventTailRequest { SinceLine = 1, MaxItems = 1 });
            Assert.Single(paged.Items);
            Assert.Equal(2, paged.Items[0].LineNumber);

            var orderedFiltered = store.TailEvents(new StateEventTailRequest { SinceLine = 0, EventType = "checkpoint.created" });
            Assert.Equal(2, orderedFiltered.Items.Count);
            Assert.Equal(3, orderedFiltered.Items[0].LineNumber);
            Assert.Equal(4, orderedFiltered.Items[1].LineNumber);

            var pagedFiltered = store.TailEvents(new StateEventTailRequest
            {
                SinceLine = 1,
                EventType = "checkpoint.created",
                MaxItems = 1
            });
            Assert.Single(pagedFiltered.Items);
            Assert.Equal(3, pagedFiltered.Items[0].LineNumber);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-state-store", Guid.NewGuid().ToString("N"));
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


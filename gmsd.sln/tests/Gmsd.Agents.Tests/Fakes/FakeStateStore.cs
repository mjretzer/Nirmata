using System.Text.Json;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IStateStore for unit testing.
/// Supports both in-memory and filesystem-backed modes.
/// </summary>
public sealed class FakeStateStore : IStateStore
{
    private StateSnapshot? _snapshot;
    private List<StateEventEntry> _events = new();
    private readonly string? _basePath;
    private readonly bool _useRealFilesystem;

    /// <summary>
    /// Creates a fake that operates in-memory only.
    /// </summary>
    public FakeStateStore()
    {
        _useRealFilesystem = false;
        _basePath = null;
    }

    /// <summary>
    /// Creates a fake that reads from the filesystem.
    /// </summary>
    public FakeStateStore(string basePath)
    {
        _basePath = basePath;
        _useRealFilesystem = true;
    }

    /// <inheritdoc />
    public StateSnapshot ReadSnapshot()
    {
        // Return in-memory snapshot if set
        if (_snapshot != null)
            return _snapshot;

        // Try to read from filesystem if configured
        if (_useRealFilesystem && _basePath != null)
        {
            var statePath = Path.Combine(_basePath, ".aos", "state", "state.json");
            if (File.Exists(statePath))
            {
                var json = File.ReadAllText(statePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json, options);
                if (snapshot != null)
                    return snapshot;
            }
        }

        // Return empty snapshot by default
        return new StateSnapshot
        {
            SchemaVersion = 1,
            Cursor = new StateCursor()
        };
    }

    /// <inheritdoc />
    public void EnsureWorkspaceInitialized()
    {
        if (!_useRealFilesystem || _basePath is null)
        {
            _snapshot ??= new StateSnapshot
            {
                SchemaVersion = 1,
                Cursor = new StateCursor()
            };
            return;
        }

        var stateDir = Path.Combine(_basePath, ".aos", "state");
        Directory.CreateDirectory(stateDir);

        var eventsPath = Path.Combine(stateDir, "events.ndjson");
        if (!File.Exists(eventsPath))
        {
            using var _ = File.Open(eventsPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        }

        var statePath = Path.Combine(stateDir, "state.json");
        if (!File.Exists(statePath))
        {
            File.WriteAllText(statePath, "{\"schemaVersion\":1,\"cursor\":{}}\n");
        }
    }

    /// <inheritdoc />
    public void AppendEvent(JsonElement payload)
    {
        // No-op for fake
    }

    /// <inheritdoc />
    public StateEventTailResponse TailEvents(StateEventTailRequest request)
    {
        var items = request.MaxItems.HasValue
            ? _events.Take(request.MaxItems.Value).ToList()
            : _events.ToList();
        return new StateEventTailResponse
        {
            Items = items
        };
    }

    /// <summary>
    /// Sets the snapshot to be returned by ReadSnapshot.
    /// </summary>
    public void SetSnapshot(StateSnapshot? snapshot)
    {
        _snapshot = snapshot;
    }

    /// <summary>
    /// Sets the events to be returned by TailEvents.
    /// </summary>
    public void SetEvents(List<StateEventEntry> events)
    {
        _events = events;
    }

    /// <summary>
    /// Resets the fake, clearing the snapshot and events.
    /// </summary>
    public void Reset()
    {
        _snapshot = null;
        _events = new List<StateEventEntry>();
    }
}

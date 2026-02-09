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
    public void AppendEvent(JsonElement payload)
    {
        // No-op for fake
    }

    /// <inheritdoc />
    public StateEventTailResponse TailEvents(StateEventTailRequest request)
    {
        return new StateEventTailResponse
        {
            Items = []
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
    /// Resets the fake, clearing the snapshot.
    /// </summary>
    public void Reset()
    {
        _snapshot = null;
    }
}

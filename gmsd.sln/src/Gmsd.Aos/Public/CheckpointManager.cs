using System.Text.Json;
using Gmsd.Aos.Engine.State;
using Gmsd.Aos.Engine.Stores;

namespace Gmsd.Aos.Public;

/// <summary>
/// Public checkpoint manager implementation for creating, restoring, and listing checkpoints.
/// </summary>
public sealed class CheckpointManager : ICheckpointManager
{
    private readonly AosStateStore _inner;
    private readonly string _checkpointsBasePath;
    private int _nextCheckpointSequence;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private CheckpointManager(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _inner = new AosStateStore(aosRootPath);
        _checkpointsBasePath = Path.Combine(aosRootPath, ".aos", "state", "checkpoints");
        _nextCheckpointSequence = DiscoverNextCheckpointSequence();
    }

    /// <summary>
    /// Creates a checkpoint manager for an explicit <c>.aos</c> root path.
    /// </summary>
    public static CheckpointManager FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates a checkpoint manager for a workspace's <c>.aos</c> root.
    /// </summary>
    public static CheckpointManager FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new CheckpointManager(workspace.AosRootPath);
    }

    /// <inheritdoc />
    public string CreateCheckpoint(string? description = null)
    {
        EnsureCheckpointsDirectoryExists();

        var checkpointId = $"CHK-{_nextCheckpointSequence:D4}";
        _nextCheckpointSequence++;

        var checkpointDir = Path.Combine(_checkpointsBasePath, checkpointId);
        Directory.CreateDirectory(checkpointDir);

        // Read current state
        var stateSnapshot = _inner.ReadStateSnapshot();

        // Write state snapshot to checkpoint
        var stateSnapshotPath = Path.Combine(checkpointDir, "state.json");
        var checkpointStatePath = $".aos/state/checkpoints/{checkpointId}/state.json";
        File.WriteAllText(stateSnapshotPath, JsonSerializer.Serialize(stateSnapshot, JsonOptions));

        // Write checkpoint metadata
        var metadata = new CheckpointMetadata
        {
            SchemaVersion = 1,
            CheckpointId = checkpointId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            StateSnapshotPath = checkpointStatePath,
            Description = description
        };

        var metadataPath = Path.Combine(checkpointDir, "checkpoint.json");
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions));

        // Append checkpoint.created event
        var evt = new
        {
            eventType = "checkpoint.created",
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            payload = new
            {
                checkpointId,
                description,
                stateSnapshotPath = checkpointStatePath
            }
        };
        _inner.AppendEvent(evt);

        return checkpointId;
    }

    /// <inheritdoc />
    public void RestoreCheckpoint(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
            throw new ArgumentException("Checkpoint ID cannot be null or whitespace.", nameof(checkpointId));

        var checkpointDir = Path.Combine(_checkpointsBasePath, checkpointId);
        if (!Directory.Exists(checkpointDir))
            throw new FileNotFoundException($"Checkpoint '{checkpointId}' not found.");

        var metadataPath = Path.Combine(checkpointDir, "checkpoint.json");
        if (!File.Exists(metadataPath))
            throw new InvalidOperationException($"Checkpoint '{checkpointId}' metadata is missing or corrupted.");

        var stateSnapshotPath = Path.Combine(checkpointDir, "state.json");
        if (!File.Exists(stateSnapshotPath))
            throw new InvalidOperationException($"Checkpoint '{checkpointId}' state snapshot is missing.");

        // Read the checkpoint state
        var checkpointStateJson = File.ReadAllText(stateSnapshotPath);
        var checkpointState = JsonSerializer.Deserialize<StateSnapshotDocument>(checkpointStateJson, JsonOptions);
        if (checkpointState is null)
            throw new InvalidOperationException($"Checkpoint '{checkpointId}' state snapshot could not be deserialized.");

        // Restore state by overwriting current state.json
        _inner.WriteStateSnapshotOverwrite(checkpointState);

        // Append checkpoint.restored event
        var evt = new
        {
            eventType = "checkpoint.restored",
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            payload = new
            {
                checkpointId,
                restoredAt = DateTimeOffset.UtcNow.ToString("O")
            }
        };
        _inner.AppendEvent(evt);
    }

    /// <inheritdoc />
    public IReadOnlyList<CheckpointInfo> ListCheckpoints()
    {
        if (!Directory.Exists(_checkpointsBasePath))
            return Array.Empty<CheckpointInfo>();

        var checkpoints = new List<CheckpointInfo>();

        foreach (var checkpointDir in Directory.GetDirectories(_checkpointsBasePath))
        {
            var checkpointId = Path.GetFileName(checkpointDir);
            var metadataPath = Path.Combine(checkpointDir, "checkpoint.json");

            if (!File.Exists(metadataPath))
                continue;

            try
            {
                var metadataJson = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize<CheckpointMetadata>(metadataJson, JsonOptions);
                if (metadata is not null)
                {
                    checkpoints.Add(new CheckpointInfo(
                        metadata.CheckpointId,
                        metadata.CreatedAtUtc,
                        metadata.Description,
                        metadata.StateSnapshotPath));
                }
            }
            catch
            {
                // Skip corrupted checkpoints
                continue;
            }
        }

        return checkpoints.OrderBy(c => c.CheckpointId).ToList();
    }

    /// <inheritdoc />
    public CheckpointInfo? GetCheckpoint(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
            throw new ArgumentException("Checkpoint ID cannot be null or whitespace.", nameof(checkpointId));

        var checkpointDir = Path.Combine(_checkpointsBasePath, checkpointId);
        var metadataPath = Path.Combine(checkpointDir, "checkpoint.json");

        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var metadataJson = File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<CheckpointMetadata>(metadataJson, JsonOptions);
            if (metadata is null)
                return null;

            return new CheckpointInfo(
                metadata.CheckpointId,
                metadata.CreatedAtUtc,
                metadata.Description,
                metadata.StateSnapshotPath);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool CheckpointExists(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
            throw new ArgumentException("Checkpoint ID cannot be null or whitespace.", nameof(checkpointId));

        var checkpointDir = Path.Combine(_checkpointsBasePath, checkpointId);
        return Directory.Exists(checkpointDir) && File.Exists(Path.Combine(checkpointDir, "checkpoint.json"));
    }

    private void EnsureCheckpointsDirectoryExists()
    {
        if (!Directory.Exists(_checkpointsBasePath))
        {
            Directory.CreateDirectory(_checkpointsBasePath);
        }
    }

    private int DiscoverNextCheckpointSequence()
    {
        if (!Directory.Exists(_checkpointsBasePath))
            return 1;

        var maxSequence = 0;
        foreach (var checkpointDir in Directory.GetDirectories(_checkpointsBasePath))
        {
            var dirName = Path.GetFileName(checkpointDir);
            if (dirName.StartsWith("CHK-", StringComparison.Ordinal) &&
                int.TryParse(dirName.AsSpan(4), out var sequence))
            {
                maxSequence = Math.Max(maxSequence, sequence);
            }
        }

        return maxSequence + 1;
    }

    private sealed class CheckpointMetadata
    {
        public int SchemaVersion { get; set; }
        public string CheckpointId { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string StateSnapshotPath { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}

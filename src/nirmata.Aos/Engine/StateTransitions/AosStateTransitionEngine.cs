using nirmata.Aos.Engine.State;
using nirmata.Aos.Engine.Stores;

namespace nirmata.Aos.Engine.StateTransitions;

/// <summary>
/// Minimal transition validator for v1 state artifacts.
/// The key guarantee for this milestone is that callers MUST validate a transition
/// before writing any state artifacts (state snapshot / event log).
/// </summary>
internal static class AosStateTransitionEngine
{
    public static AosStateTransitionTable.Rule ValidateTransitionOrThrow(
        string aosRootPath,
        string kind,
        string? checkpointId = null)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(kind));

        if (!AosStateTransitionTable.Rules.TryGetValue(kind, out var rule))
        {
            throw new AosInvalidStateTransitionException(kind, "Unknown transition kind.");
        }

        // Validate the current snapshot unless this is initial seeding.
        var stateStore = new AosStateStore(aosRootPath);
        if (!string.Equals(kind, AosStateTransitionTable.Kinds.StateInitialized, StringComparison.Ordinal))
        {
            _ = stateStore.ReadStateSnapshot();
        }

        if (rule.RequiresCheckpoint)
        {
            if (string.IsNullOrWhiteSpace(checkpointId))
            {
                throw new AosInvalidStateTransitionException(kind, "Missing required checkpointId.");
            }

            var checkpointSnapshotPath = Path.Combine(aosRootPath, "state", "checkpoints", checkpointId, "state.json");
            if (!File.Exists(checkpointSnapshotPath))
            {
                throw new AosInvalidStateTransitionException(kind, $"Checkpoint snapshot not found: {checkpointSnapshotPath}");
            }
        }

        return rule;
    }

    /// <summary>
    /// Ensures the baseline state artifacts exist. If a write is required, it is gated by the
    /// <c>state.initialized</c> transition rule.
    /// </summary>
    public static void EnsureStateInitialized(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));

        var statePath = Path.Combine(aosRootPath, "state", "state.json");
        var eventsPath = Path.Combine(aosRootPath, "state", "events.ndjson");
        if (File.Exists(statePath) && File.Exists(eventsPath))
        {
            return;
        }

        _ = ValidateTransitionOrThrow(aosRootPath, AosStateTransitionTable.Kinds.StateInitialized);

        var stateStore = new AosStateStore(aosRootPath);
        stateStore.WriteStateSnapshotIfMissing(
            new StateSnapshotDocument(
                SchemaVersion: 1,
                Cursor: new StateCursorDocument()
            )
        );
        stateStore.EnsureEventsLogExists();
    }
}

internal sealed class AosInvalidStateTransitionException : InvalidOperationException
{
    public AosInvalidStateTransitionException(string kind, string message)
        : base($"Invalid state transition '{kind}': {message}")
    {
    }
}


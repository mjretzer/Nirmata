namespace nirmata.Aos.Public;

using System;
using System.Text.Json;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Engine.State;
using nirmata.Aos.Engine.Stores;

/// <summary>
/// Public state store implementation backed by the internal engine store.
/// </summary>
public sealed class StateStore : IStateStore
{
    private readonly AosStateStore _inner;

    private StateStore(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _inner = new AosStateStore(aosRootPath);
    }

    /// <summary>
    /// Creates a state store for an explicit <c>.aos</c> root path.
    /// </summary>
    public static StateStore FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates a state store for a workspace's <c>.aos</c> root.
    /// </summary>
    public static StateStore FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new StateStore(workspace.AosRootPath);
    }

    public void EnsureWorkspaceInitialized()
    {
        _inner.EnsureEventsLogExists();

        var stateContractPath = Path.Combine(_inner.AosRootPath, "state", "state.json");
        if (!File.Exists(stateContractPath))
        {
            _inner.DeriveAndWriteStateSnapshotFromEventsOverwrite();
            return;
        }

        var current = _inner.ReadStateSnapshot();
        var derived = _inner.DeriveStateSnapshotFromEvents();
        if (current != derived)
        {
            _inner.WriteStateSnapshotOverwrite(derived);
        }
    }

    public StateSnapshot ReadSnapshot()
    {
        var doc = _inner.ReadStateSnapshot();
        return ToContract(doc);
    }

    public void AppendEvent(JsonElement payload) => _inner.AppendEvent(payload);

    public StateEventTailResponse TailEvents(StateEventTailRequest request) => _inner.TailEvents(request);

    private static StateSnapshot ToContract(StateSnapshotDocument doc)
    {
        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported state snapshot schemaVersion '{doc.SchemaVersion}'.");
        }

        return new StateSnapshot
        {
            SchemaVersion = doc.SchemaVersion,
            Cursor = ToContract(doc.Cursor)
        };
    }

    private static StateCursor ToContract(StateCursorDocument? cursor)
    {
        if (cursor is null)
        {
            return new StateCursor();
        }

        return new StateCursor
        {
            MilestoneId = cursor.MilestoneId,
            PhaseId = cursor.PhaseId,
            TaskId = cursor.TaskId,
            StepId = cursor.StepId,
            MilestoneStatus = cursor.MilestoneStatus,
            PhaseStatus = cursor.PhaseStatus,
            TaskStatus = cursor.TaskStatus,
            StepStatus = cursor.StepStatus,
            Kind = cursor.Kind,
            Id = cursor.Id
        };
    }
}


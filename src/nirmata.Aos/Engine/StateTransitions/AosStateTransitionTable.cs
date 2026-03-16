using System.Collections.ObjectModel;

namespace nirmata.Aos.Engine.StateTransitions;

/// <summary>
/// Minimal state transition table for the v1 "cursor" model.
///
/// Cursor model (v1):
/// - <c>.aos/state/state.json</c> has <c>schemaVersion = 1</c>
/// - <c>cursor</c> is intentionally schema-light (an object with arbitrary properties)
///
/// Because the cursor shape is intentionally opaque in v1, transitions are keyed by
/// transition <c>kind</c> and validated via operation-specific preconditions (e.g. a
/// checkpoint restore requires a checkpoint).
/// </summary>
internal static class AosStateTransitionTable
{
    public static class Kinds
    {
        /// <summary>
        /// Baseline initialization of <c>.aos/state/state.json</c> by workspace bootstrap.
        /// </summary>
        public const string StateInitialized = "state.initialized";

        /// <summary>
        /// A checkpoint was created under <c>.aos/state/checkpoints/**</c>.
        /// </summary>
        public const string CheckpointCreated = "checkpoint.created";

        /// <summary>
        /// A checkpoint was restored, rolling back <c>.aos/state/state.json</c>.
        /// </summary>
        public const string CheckpointRestored = "checkpoint.restored";
    }

    /// <summary>
    /// Describes a transition "kind" at the level of the minimal cursor model.
    /// </summary>
    public sealed record Rule(
        string Kind,
        bool MutatesStateSnapshot,
        bool AppendsEvent,
        bool RequiresCheckpoint,
        string Description);

    /// <summary>
    /// The minimal transition table for the current cursor model.
    /// </summary>
    public static IReadOnlyDictionary<string, Rule> Rules { get; } =
        new ReadOnlyDictionary<string, Rule>(
            new Dictionary<string, Rule>(StringComparer.Ordinal)
            {
                // Workspace bootstrap / seeding. Not a rollback.
                [Kinds.StateInitialized] = new Rule(
                    Kind: Kinds.StateInitialized,
                    MutatesStateSnapshot: true,
                    AppendsEvent: false,
                    RequiresCheckpoint: false,
                    Description: "Seeds baseline state snapshot (schemaVersion=1, cursor={})."
                ),

                // Forward action: snapshot is stored under checkpoints; state.json is not changed.
                [Kinds.CheckpointCreated] = new Rule(
                    Kind: Kinds.CheckpointCreated,
                    MutatesStateSnapshot: false,
                    AppendsEvent: true,
                    RequiresCheckpoint: false,
                    Description: "Snapshots current state into a new checkpoint and appends an audit event."
                ),

                // Rollback action: requires a checkpoint and overwrites state.json to match it.
                [Kinds.CheckpointRestored] = new Rule(
                    Kind: Kinds.CheckpointRestored,
                    MutatesStateSnapshot: true,
                    AppendsEvent: true,
                    RequiresCheckpoint: true,
                    Description: "Rolls back state.json from a checkpoint snapshot and appends an audit event."
                )
            });
}


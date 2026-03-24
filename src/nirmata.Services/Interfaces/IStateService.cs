using nirmata.Data.Dto.Models.State;
namespace nirmata.Services.Interfaces;

public interface IStateService
{
    /// <summary>Reads the continuity state from <c>.aos/state/state.json</c> under the given workspace root. Returns <c>null</c> if the file does not exist.</summary>
    Task<ContinuityStateDto?> GetStateAsync(string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>Reads the pause/resume handoff snapshot from <c>.aos/state/handoff.json</c>. Returns <c>null</c> if the file does not exist.</summary>
    Task<HandoffSnapshotDto?> GetHandoffAsync(string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>Tails the most recent events from <c>.aos/state/events.ndjson</c>. Returns an empty list if the file does not exist.</summary>
    Task<IReadOnlyList<StateEventDto>> GetEventsAsync(string workspaceRoot, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>Lists checkpoint summaries from <c>.aos/state/checkpoints/**</c>, newest first. Returns an empty list if the directory does not exist.</summary>
    Task<IReadOnlyList<CheckpointSummaryDto>> GetCheckpointsAsync(string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>Lists context-pack summaries from <c>.aos/context/packs/**</c>. Returns an empty list if the directory does not exist.</summary>
    Task<IReadOnlyList<ContextPackSummaryDto>> GetContextPacksAsync(string workspaceRoot, CancellationToken cancellationToken = default);
}

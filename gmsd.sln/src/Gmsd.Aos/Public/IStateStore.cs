namespace Gmsd.Aos.Public;

using System.Text.Json;
using Gmsd.Aos.Contracts.State;

/// <summary>
/// Public state store abstraction (compile-time contract).
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Ensures baseline workspace state artifacts exist before runtime state usage.
    /// Creates deterministic defaults for missing state artifacts.
    /// </summary>
    void EnsureWorkspaceInitialized();

    /// <summary>
    /// Reads the current operational state snapshot from <c>.aos/state/state.json</c>.
    /// </summary>
    StateSnapshot ReadSnapshot();

    /// <summary>
    /// Appends a JSON object event to <c>.aos/state/events.ndjson</c> (NDJSON).
    /// </summary>
    void AppendEvent(JsonElement payload);

    /// <summary>
    /// Tails an ordered slice of <c>.aos/state/events.ndjson</c> without re-sorting.
    /// </summary>
    StateEventTailResponse TailEvents(StateEventTailRequest request);
}


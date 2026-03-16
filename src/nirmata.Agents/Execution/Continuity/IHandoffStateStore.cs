using nirmata.Aos.Contracts.State;

namespace nirmata.Agents.Execution.Continuity;

/// <summary>
/// Stores and retrieves handoff state from <c>.aos/state/handoff.json</c>.
/// </summary>
public interface IHandoffStateStore
{
    /// <summary>
    /// Gets the full path to the handoff.json file.
    /// </summary>
    string HandoffPath { get; }

    /// <summary>
    /// Checks if handoff.json exists.
    /// </summary>
    /// <returns>True if the handoff file exists.</returns>
    bool Exists();

    /// <summary>
    /// Reads and deserializes the handoff state from disk.
    /// </summary>
    /// <returns>The handoff state.</returns>
    /// <exception cref="FileNotFoundException">Thrown when handoff.json does not exist.</exception>
    HandoffState ReadHandoff();

    /// <summary>
    /// Serializes and writes the handoff state to disk using deterministic JSON.
    /// </summary>
    /// <param name="handoff">The handoff state to write.</param>
    void WriteHandoff(HandoffState handoff);

    /// <summary>
    /// Deletes the handoff file if it exists.
    /// </summary>
    void DeleteHandoff();
}

using Gmsd.Aos.Public.Context.Packs;

namespace Gmsd.Agents.Execution.Context;

/// <summary>
/// Manages the creation and persistence of context packs.
/// </summary>
public interface IContextPackManager
{
    /// <summary>
    /// Builds and persists a context pack for a given mode and driving ID.
    /// </summary>
    /// <param name="mode">The context pack mode (e.g., "task", "phase").</param>
    /// <param name="drivingId">The ID of the artifact driving the context pack creation.</param>
    /// <param name="budget">The budget for the context pack.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The ID of the created context pack.</returns>
    Task<string> CreatePackAsync(string mode, string drivingId, ContextPackBudget budget, CancellationToken ct = default);
}

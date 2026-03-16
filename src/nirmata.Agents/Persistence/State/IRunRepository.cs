using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Models.Results;

namespace nirmata.Agents.Persistence.State;

/// <summary>
/// Abstract repository for run storage.
/// </summary>
public interface IRunRepository
{
    /// <summary>
    /// Saves a run context.
    /// </summary>
    Task SaveAsync(RunContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a run response.
    /// </summary>
    Task SaveAsync(RunResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a run response by ID.
    /// </summary>
    Task<RunResponse?> GetAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists runs with optional filtering.
    /// </summary>
    Task<IReadOnlyList<RunResponse>> ListAsync(
        string? workflowName = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a run exists.
    /// </summary>
    Task<bool> ExistsAsync(string runId, CancellationToken cancellationToken = default);
}

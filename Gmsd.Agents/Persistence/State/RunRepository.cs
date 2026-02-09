using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Models.Results;

namespace Gmsd.Agents.Persistence.State;

/// <summary>
/// Run repository implementation that wraps Engine stores.
/// </summary>
public sealed class RunRepository : IRunRepository
{
    private readonly Dictionary<string, RunResponse> _responses = new();
    private readonly Dictionary<string, RunContext> _contexts = new();

    /// <inheritdoc />
    public Task SaveAsync(RunContext context, CancellationToken cancellationToken = default)
    {
        _contexts[context.RunId] = context;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveAsync(RunResponse response, CancellationToken cancellationToken = default)
    {
        _responses[response.RunId] = response;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RunResponse?> GetAsync(string runId, CancellationToken cancellationToken = default)
    {
        _responses.TryGetValue(runId, out var response);
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RunResponse>> ListAsync(
        string? workflowName = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        CancellationToken cancellationToken = default)
    {
        var query = _responses.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(workflowName))
        {
            // Note: WorkflowName is not stored in RunResponse currently
            // This would need to be enhanced to filter properly
        }

        if (since.HasValue)
        {
            query = query.Where(r => r.StartedAt >= since.Value);
        }

        if (until.HasValue)
        {
            query = query.Where(r => r.StartedAt <= until.Value);
        }

        return Task.FromResult<IReadOnlyList<RunResponse>>(query.ToList());
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string runId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_responses.ContainsKey(runId));
    }
}

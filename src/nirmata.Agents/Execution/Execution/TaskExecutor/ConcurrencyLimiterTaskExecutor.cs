using nirmata.Aos.Concurrency;
using Microsoft.Extensions.Logging;

namespace nirmata.Agents.Execution.Execution.TaskExecutor;

/// <summary>
/// Wraps ITaskExecutor with concurrency limiting.
/// </summary>
public sealed class ConcurrencyLimiterTaskExecutor : ITaskExecutor
{
    private readonly ITaskExecutor _innerExecutor;
    private readonly IConcurrencyLimiter _concurrencyLimiter;
    private readonly ILogger<ConcurrencyLimiterTaskExecutor> _logger;

    public ConcurrencyLimiterTaskExecutor(
        ITaskExecutor innerExecutor,
        IConcurrencyLimiter concurrencyLimiter,
        ILogger<ConcurrencyLimiterTaskExecutor> logger)
    {
        _innerExecutor = innerExecutor ?? throw new ArgumentNullException(nameof(innerExecutor));
        _concurrencyLimiter = concurrencyLimiter ?? throw new ArgumentNullException(nameof(concurrencyLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var acquired = await _concurrencyLimiter.TryAcquireTaskSlotAsync(request.TaskId, ct);
        if (!acquired)
        {
            var metrics = _concurrencyLimiter.GetMetrics();
            var errorMessage = $"Task queue is full. Current queue depth: {metrics.QueuedTaskCount}, " +
                             $"max queue size: {_concurrencyLimiter.GetMaxQueueSize()}. " +
                             $"Please retry after some tasks complete.";
            
            _logger.LogWarning("Task slot acquisition failed: {ErrorMessage}", errorMessage);
            
            return new TaskExecutionResult
            {
                Success = false,
                RunId = string.Empty,
                ErrorMessage = errorMessage,
                ModifiedFiles = Array.Empty<string>(),
                EvidenceArtifacts = Array.Empty<string>(),
                ResultData = new Dictionary<string, object>
                {
                    ["queueFull"] = true,
                    ["currentQueueDepth"] = metrics.QueuedTaskCount,
                    ["maxQueueSize"] = _concurrencyLimiter.GetMaxQueueSize()
                }
            };
        }

        try
        {
            var result = await _innerExecutor.ExecuteAsync(request, ct);
            return result;
        }
        finally
        {
            _concurrencyLimiter.ReleaseTaskSlot(request.TaskId);
        }
    }
}

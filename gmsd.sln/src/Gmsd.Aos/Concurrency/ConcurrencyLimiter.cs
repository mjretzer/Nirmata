using Gmsd.Aos.Configuration;
using Microsoft.Extensions.Logging;

namespace Gmsd.Aos.Concurrency;

/// <summary>
/// Manages concurrency limits for task execution and LLM calls.
/// </summary>
public sealed class ConcurrencyLimiter : IConcurrencyLimiter
{
    private readonly ConcurrencyOptions _options;
    private readonly ILogger<ConcurrencyLimiter> _logger;
    private readonly SemaphoreSlim _taskSlotSemaphore;
    private readonly SemaphoreSlim _llmCallSlotSemaphore;
    private readonly Queue<string> _taskQueue = new();
    private readonly object _lockObject = new();
    private int _activeTaskCount;
    private int _activeLlmCallCount;
    private long _totalTasksCompleted;
    private DateTime _lastCompletionTime = DateTime.UtcNow;

    public ConcurrencyLimiter(ConcurrencyOptions options, ILogger<ConcurrencyLimiter> logger)
    {
        var validationError = options.Validate();
        if (validationError != null)
        {
            throw new ArgumentException(validationError, nameof(options));
        }

        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _taskSlotSemaphore = new SemaphoreSlim(options.MaxParallelTasks, options.MaxParallelTasks);
        _llmCallSlotSemaphore = new SemaphoreSlim(options.MaxParallelLlmCalls, options.MaxParallelLlmCalls);

        _logger.LogInformation(
            "ConcurrencyLimiter initialized: maxParallelTasks={MaxParallelTasks}, maxParallelLlmCalls={MaxParallelLlmCalls}, taskQueueSize={TaskQueueSize}",
            options.MaxParallelTasks,
            options.MaxParallelLlmCalls,
            options.TaskQueueSize);
    }

    public ConcurrencyMetrics GetMetrics()
    {
        lock (_lockObject)
        {
            var queueDepthPercentage = _options.TaskQueueSize > 0
                ? (_taskQueue.Count / (double)_options.TaskQueueSize) * 100
                : 0;

            var timeSinceLastCompletion = DateTime.UtcNow - _lastCompletionTime;
            var taskCompletionRate = timeSinceLastCompletion.TotalMinutes > 0
                ? _totalTasksCompleted / timeSinceLastCompletion.TotalMinutes
                : 0;

            return new ConcurrencyMetrics
            {
                ActiveTaskCount = _activeTaskCount,
                QueuedTaskCount = _taskQueue.Count,
                ActiveLlmCallCount = _activeLlmCallCount,
                QueueDepthPercentage = queueDepthPercentage,
                TaskCompletionRate = taskCompletionRate,
                TotalTasksCompleted = _totalTasksCompleted,
                CapturedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> TryAcquireTaskSlotAsync(string taskId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        lock (_lockObject)
        {
            if (_taskQueue.Count >= _options.TaskQueueSize)
            {
                var metrics = GetMetrics();
                _logger.LogWarning(
                    "Task queue full: taskId={TaskId}, queueDepth={QueueDepth}, maxQueueSize={MaxQueueSize}",
                    taskId,
                    metrics.QueuedTaskCount,
                    _options.TaskQueueSize);
                return false;
            }

            _taskQueue.Enqueue(taskId);
        }

        var acquired = await _taskSlotSemaphore.WaitAsync(0, ct);
        if (acquired)
        {
            lock (_lockObject)
            {
                _taskQueue.Dequeue();
                _activeTaskCount++;
            }

            _logger.LogDebug(
                "Task slot acquired: taskId={TaskId}, activeTaskCount={ActiveTaskCount}",
                taskId,
                _activeTaskCount);
        }

        return acquired;
    }

    public void ReleaseTaskSlot(string taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        lock (_lockObject)
        {
            if (_activeTaskCount > 0)
            {
                _activeTaskCount--;
                _totalTasksCompleted++;
                _lastCompletionTime = DateTime.UtcNow;
            }
        }

        _taskSlotSemaphore.Release();

        _logger.LogDebug(
            "Task slot released: taskId={TaskId}, activeTaskCount={ActiveTaskCount}",
            taskId,
            _activeTaskCount);
    }

    public async Task<bool> TryAcquireLlmCallSlotAsync(CancellationToken ct = default)
    {
        var acquired = await _llmCallSlotSemaphore.WaitAsync(0, ct);
        if (acquired)
        {
            lock (_lockObject)
            {
                _activeLlmCallCount++;
            }

            _logger.LogDebug(
                "LLM call slot acquired: activeLlmCallCount={ActiveLlmCallCount}",
                _activeLlmCallCount);
        }
        else
        {
            _logger.LogWarning(
                "LLM call rate limit reached: activeLlmCallCount={ActiveLlmCallCount}, maxParallelLlmCalls={MaxParallelLlmCalls}",
                _activeLlmCallCount,
                _options.MaxParallelLlmCalls);
        }

        return acquired;
    }

    public void ReleaseLlmCallSlot()
    {
        lock (_lockObject)
        {
            if (_activeLlmCallCount > 0)
            {
                _activeLlmCallCount--;
            }
        }

        _llmCallSlotSemaphore.Release();

        _logger.LogDebug(
            "LLM call slot released: activeLlmCallCount={ActiveLlmCallCount}",
            _activeLlmCallCount);
    }

    public int GetQueueDepth()
    {
        lock (_lockObject)
        {
            return _taskQueue.Count;
        }
    }

    public int GetMaxQueueSize() => _options.TaskQueueSize;

    public int GetMaxParallelTasks() => _options.MaxParallelTasks;

    public int GetMaxParallelLlmCalls() => _options.MaxParallelLlmCalls;
}

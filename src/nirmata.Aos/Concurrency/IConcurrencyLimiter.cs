namespace nirmata.Aos.Concurrency;

/// <summary>
/// Manages concurrency limits for task execution and LLM calls.
/// </summary>
public interface IConcurrencyLimiter
{
    /// <summary>
    /// Gets the current concurrency metrics.
    /// </summary>
    ConcurrencyMetrics GetMetrics();

    /// <summary>
    /// Attempts to acquire a slot for task execution.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if slot acquired, false if queue is full.</returns>
    Task<bool> TryAcquireTaskSlotAsync(string taskId, CancellationToken ct = default);

    /// <summary>
    /// Releases a task execution slot.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    void ReleaseTaskSlot(string taskId);

    /// <summary>
    /// Attempts to acquire a slot for an LLM call.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if slot acquired, false if limit reached.</returns>
    Task<bool> TryAcquireLlmCallSlotAsync(CancellationToken ct = default);

    /// <summary>
    /// Releases an LLM call slot.
    /// </summary>
    void ReleaseLlmCallSlot();

    /// <summary>
    /// Gets the current queue depth.
    /// </summary>
    int GetQueueDepth();

    /// <summary>
    /// Gets the maximum queue size.
    /// </summary>
    int GetMaxQueueSize();

    /// <summary>
    /// Gets the maximum parallel tasks.
    /// </summary>
    int GetMaxParallelTasks();

    /// <summary>
    /// Gets the maximum parallel LLM calls.
    /// </summary>
    int GetMaxParallelLlmCalls();
}

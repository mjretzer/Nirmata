namespace Gmsd.Aos.Concurrency;

/// <summary>
/// Metrics for concurrency monitoring and observability.
/// </summary>
public sealed class ConcurrencyMetrics
{
    /// <summary>
    /// Current number of tasks executing in parallel.
    /// </summary>
    public int ActiveTaskCount { get; set; }

    /// <summary>
    /// Current number of tasks waiting in the queue.
    /// </summary>
    public int QueuedTaskCount { get; set; }

    /// <summary>
    /// Current number of active LLM calls.
    /// </summary>
    public int ActiveLlmCallCount { get; set; }

    /// <summary>
    /// Queue depth as a percentage of maximum queue size.
    /// </summary>
    public double QueueDepthPercentage { get; set; }

    /// <summary>
    /// Number of tasks completed per minute.
    /// </summary>
    public double TaskCompletionRate { get; set; }

    /// <summary>
    /// Total number of tasks completed since startup.
    /// </summary>
    public long TotalTasksCompleted { get; set; }

    /// <summary>
    /// Timestamp when metrics were captured.
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

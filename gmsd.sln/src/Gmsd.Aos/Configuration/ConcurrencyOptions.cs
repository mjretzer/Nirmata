namespace Gmsd.Aos.Configuration;

/// <summary>
/// Configuration options for concurrency limits and rate limiting.
/// </summary>
public sealed class ConcurrencyOptions
{
    /// <summary>
    /// Maximum number of tasks that can execute in parallel.
    /// Default: 3
    /// </summary>
    public int MaxParallelTasks { get; set; } = 3;

    /// <summary>
    /// Maximum number of concurrent LLM provider calls.
    /// Default: 2
    /// </summary>
    public int MaxParallelLlmCalls { get; set; } = 2;

    /// <summary>
    /// Maximum number of tasks that can be queued waiting for execution.
    /// Default: 10
    /// </summary>
    public int TaskQueueSize { get; set; } = 10;

    /// <summary>
    /// Validates the concurrency configuration.
    /// </summary>
    /// <returns>Validation error message, or null if valid.</returns>
    public string? Validate()
    {
        if (MaxParallelTasks < 1)
        {
            return $"maxParallelTasks must be >= 1, got {MaxParallelTasks}";
        }

        if (MaxParallelLlmCalls < 1)
        {
            return $"maxParallelLlmCalls must be >= 1, got {MaxParallelLlmCalls}";
        }

        if (TaskQueueSize < 1)
        {
            return $"taskQueueSize must be >= 1, got {TaskQueueSize}";
        }

        if (TaskQueueSize < MaxParallelTasks)
        {
            return $"taskQueueSize ({TaskQueueSize}) must be >= maxParallelTasks ({MaxParallelTasks})";
        }

        return null;
    }
}

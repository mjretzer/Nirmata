using nirmata.Aos.Configuration;
using nirmata.Aos.Concurrency;
using Microsoft.Extensions.Logging;
using Xunit;

namespace nirmata.Aos.Tests.Concurrency;

public class ConcurrencyLimiterTests
{
    private readonly ILogger<ConcurrencyLimiter> _logger;

    public ConcurrencyLimiterTests()
    {
        _logger = new LoggerFactory().CreateLogger<ConcurrencyLimiter>();
    }

    [Fact]
    public void Constructor_WithInvalidMaxParallelTasks_ThrowsArgumentException()
    {
        var options = new ConcurrencyOptions { MaxParallelTasks = 0 };
        Assert.Throws<ArgumentException>(() => new ConcurrencyLimiter(options, _logger));
    }

    [Fact]
    public void Constructor_WithInvalidMaxParallelLlmCalls_ThrowsArgumentException()
    {
        var options = new ConcurrencyOptions { MaxParallelLlmCalls = 0 };
        Assert.Throws<ArgumentException>(() => new ConcurrencyLimiter(options, _logger));
    }

    [Fact]
    public void Constructor_WithTaskQueueSizeLessThanMaxParallelTasks_ThrowsArgumentException()
    {
        var options = new ConcurrencyOptions 
        { 
            MaxParallelTasks = 5,
            TaskQueueSize = 3
        };
        Assert.Throws<ArgumentException>(() => new ConcurrencyLimiter(options, _logger));
    }

    [Fact]
    public async Task TryAcquireTaskSlotAsync_WithAvailableSlot_ReturnsTrue()
    {
        var options = new ConcurrencyOptions { MaxParallelTasks = 2, TaskQueueSize = 5 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        var result = await limiter.TryAcquireTaskSlotAsync("task-1");
        Assert.True(result);
    }

    [Fact]
    public async Task TryAcquireTaskSlotAsync_WhenQueueFull_ReturnsFalse()
    {
        var options = new ConcurrencyOptions { MaxParallelTasks = 2, TaskQueueSize = 2 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        // Acquire 2 slots
        await limiter.TryAcquireTaskSlotAsync("task-1");
        await limiter.TryAcquireTaskSlotAsync("task-2");

        // Queue 2 more
        await limiter.TryAcquireTaskSlotAsync("task-3");
        await limiter.TryAcquireTaskSlotAsync("task-4");

        // 5th should fail
        var result = await limiter.TryAcquireTaskSlotAsync("task-5");
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseTaskSlot_DecreasesActiveTaskCount()
    {
        var options = new ConcurrencyOptions { MaxParallelTasks = 2, TaskQueueSize = 5 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        await limiter.TryAcquireTaskSlotAsync("task-1");
        var metricsAfterAcquire = limiter.GetMetrics();
        Assert.Equal(1, metricsAfterAcquire.ActiveTaskCount);

        limiter.ReleaseTaskSlot("task-1");
        var metricsAfterRelease = limiter.GetMetrics();
        Assert.Equal(0, metricsAfterRelease.ActiveTaskCount);
    }

    [Fact]
    public async Task TryAcquireLlmCallSlotAsync_WithAvailableSlot_ReturnsTrue()
    {
        var options = new ConcurrencyOptions { MaxParallelLlmCalls = 2 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        var result = await limiter.TryAcquireLlmCallSlotAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task TryAcquireLlmCallSlotAsync_WhenLimitReached_ReturnsFalse()
    {
        var options = new ConcurrencyOptions { MaxParallelLlmCalls = 2 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        await limiter.TryAcquireLlmCallSlotAsync();
        await limiter.TryAcquireLlmCallSlotAsync();

        var result = await limiter.TryAcquireLlmCallSlotAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseLlmCallSlot_DecreasesActiveLlmCallCount()
    {
        var options = new ConcurrencyOptions { MaxParallelLlmCalls = 2 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        await limiter.TryAcquireLlmCallSlotAsync();
        var metricsAfterAcquire = limiter.GetMetrics();
        Assert.Equal(1, metricsAfterAcquire.ActiveLlmCallCount);

        limiter.ReleaseLlmCallSlot();
        var metricsAfterRelease = limiter.GetMetrics();
        Assert.Equal(0, metricsAfterRelease.ActiveLlmCallCount);
    }

    [Fact]
    public async Task GetMetrics_ReturnsAccurateMetrics()
    {
        var options = new ConcurrencyOptions 
        { 
            MaxParallelTasks = 2,
            TaskQueueSize = 5,
            MaxParallelLlmCalls = 2
        };
        var limiter = new ConcurrencyLimiter(options, _logger);

        await limiter.TryAcquireTaskSlotAsync("task-1");
        await limiter.TryAcquireTaskSlotAsync("task-2");
        await limiter.TryAcquireLlmCallSlotAsync();

        var metrics = limiter.GetMetrics();
        Assert.Equal(2, metrics.ActiveTaskCount);
        Assert.Equal(1, metrics.ActiveLlmCallCount);
        Assert.True(metrics.QueueDepthPercentage >= 0);
    }

    [Fact]
    public void GetMaxParallelTasks_ReturnsConfiguredValue()
    {
        var options = new ConcurrencyOptions { MaxParallelTasks = 5 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        Assert.Equal(5, limiter.GetMaxParallelTasks());
    }

    [Fact]
    public void GetMaxParallelLlmCalls_ReturnsConfiguredValue()
    {
        var options = new ConcurrencyOptions { MaxParallelLlmCalls = 3 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        Assert.Equal(3, limiter.GetMaxParallelLlmCalls());
    }

    [Fact]
    public void GetMaxQueueSize_ReturnsConfiguredValue()
    {
        var options = new ConcurrencyOptions { TaskQueueSize = 10 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        Assert.Equal(10, limiter.GetMaxQueueSize());
    }

    [Fact]
    public async Task ConcurrentTaskAcquisition_EnforcesLimit()
    {
        var options = new ConcurrencyOptions { MaxParallelTasks = 3, TaskQueueSize = 10 };
        var limiter = new ConcurrencyLimiter(options, _logger);

        var tasks = new List<Task<bool>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(limiter.TryAcquireTaskSlotAsync($"task-{i}"));
        }

        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r);
        Assert.Equal(3, successCount);
    }
}

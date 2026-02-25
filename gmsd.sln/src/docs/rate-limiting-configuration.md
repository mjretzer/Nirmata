# Rate Limiting and Concurrency Configuration Guide

## Overview

Rate limiting and concurrency bounds prevent unbounded parallel execution that could overwhelm system resources. This guide explains how to configure and tune these limits.

## Configuration File

Rate limiting is configured in `.aos/config/concurrency.json`:

```json
{
  "maxParallelTasks": 3,
  "maxParallelLlmCalls": 2,
  "taskQueueSize": 10
}
```

## Configuration Parameters

### maxParallelTasks

**Purpose:** Maximum number of tasks that can execute in parallel

**Default:** 3

**Range:** 1-100

**Impact:**
- Higher values = more parallelism, higher resource usage
- Lower values = less parallelism, lower resource usage
- Affects task executor queue depth

**Example:**
```json
{
  "maxParallelTasks": 5
}
```

### maxParallelLlmCalls

**Purpose:** Maximum number of concurrent LLM API calls

**Default:** 2

**Range:** 1-50

**Impact:**
- Higher values = more concurrent API calls, higher API costs
- Lower values = fewer concurrent calls, slower execution
- Affects LLM provider rate limiting

**Example:**
```json
{
  "maxParallelLlmCalls": 3
}
```

### taskQueueSize

**Purpose:** Maximum number of pending tasks in the queue

**Default:** 10

**Range:** 1-1000

**Impact:**
- Higher values = more memory usage for queue
- Lower values = tasks rejected if queue is full
- Prevents unbounded memory growth

**Example:**
```json
{
  "taskQueueSize": 20
}
```

## Default Configuration

```json
{
  "maxParallelTasks": 3,
  "maxParallelLlmCalls": 2,
  "taskQueueSize": 10
}
```

These defaults are suitable for:
- Single-machine deployments
- Moderate workloads
- Standard system resources (8GB RAM, 4 CPU cores)

## Tuning Guidelines

### For Development

```json
{
  "maxParallelTasks": 1,
  "maxParallelLlmCalls": 1,
  "taskQueueSize": 5
}
```

**Rationale:**
- Single-threaded execution for easier debugging
- Minimal resource usage
- Deterministic behavior

### For Staging

```json
{
  "maxParallelTasks": 2,
  "maxParallelLlmCalls": 1,
  "taskQueueSize": 10
}
```

**Rationale:**
- Limited parallelism for testing
- Moderate resource usage
- Good balance between speed and stability

### For Production (Standard)

```json
{
  "maxParallelTasks": 3,
  "maxParallelLlmCalls": 2,
  "taskQueueSize": 10
}
```

**Rationale:**
- Reasonable parallelism
- Moderate resource usage
- Good throughput

### For Production (High-Performance)

```json
{
  "maxParallelTasks": 8,
  "maxParallelLlmCalls": 5,
  "taskQueueSize": 50
}
```

**Rationale:**
- High parallelism for maximum throughput
- Higher resource usage
- Requires monitoring and tuning

**Requirements:**
- 16GB+ RAM
- 8+ CPU cores
- High-bandwidth network
- LLM provider supports high concurrency

### For Production (Low-Resource)

```json
{
  "maxParallelTasks": 1,
  "maxParallelLlmCalls": 1,
  "taskQueueSize": 5
}
```

**Rationale:**
- Minimal resource usage
- Suitable for constrained environments
- Slower execution but stable

**Use cases:**
- Edge devices
- Embedded systems
- Shared hosting

## Monitoring and Metrics

### View Current Configuration

```bash
# Display current limits
aos config show concurrency

# Output:
# Max Parallel Tasks: 3
# Max Parallel LLM Calls: 2
# Task Queue Size: 10
```

### Monitor Queue Depth

```bash
# View queue metrics
aos metrics concurrency

# Output:
# Queue Depth: 2/10 (20% full)
# Active Tasks: 3/3 (100% utilized)
# Active LLM Calls: 1/2 (50% utilized)
# Average Queue Wait: 150ms
# Max Queue Wait: 2500ms
```

### Track Over Time

```bash
# Export metrics for analysis
aos metrics concurrency --export csv > concurrency-metrics.csv

# Analyze trends
cat concurrency-metrics.csv | 
  awk -F',' '{print $1, $2}' | 
  sort | uniq -c
```

## Tuning Process

### Step 1: Establish Baseline

1. Run with default configuration
2. Monitor metrics for 24 hours
3. Record queue depth, active tasks, wait times

```bash
# Monitor baseline
while true; do
    aos metrics concurrency
    Start-Sleep -Seconds 60
done
```

### Step 2: Identify Bottlenecks

Analyze metrics to find:
- **Queue frequently full?** → Increase `maxParallelTasks`
- **CPU underutilized?** → Increase `maxParallelTasks`
- **Memory high?** → Decrease `taskQueueSize`
- **LLM rate-limited?** → Decrease `maxParallelLlmCalls`

### Step 3: Adjust Configuration

Make incremental changes:
```json
{
  "maxParallelTasks": 4,
  "maxParallelLlmCalls": 2,
  "taskQueueSize": 10
}
```

### Step 4: Monitor Impact

Run for 24 hours and compare:
- Queue depth
- Task throughput
- Resource usage
- Error rates

### Step 5: Iterate

Repeat steps 2-4 until optimal configuration is found.

## Performance Tuning Examples

### Example 1: Queue Frequently Full

**Symptom:**
```
Queue Depth: 10/10 (100% full)
Rejected Tasks: 5
```

**Analysis:**
- Queue is at capacity
- Tasks are being rejected
- Need more parallelism

**Solution:**
```json
{
  "maxParallelTasks": 5,
  "maxParallelLlmCalls": 2,
  "taskQueueSize": 10
}
```

**Result:**
```
Queue Depth: 3/10 (30% full)
Rejected Tasks: 0
```

### Example 2: CPU Underutilized

**Symptom:**
```
CPU Usage: 20%
Active Tasks: 1/3
Queue Depth: 0/10
```

**Analysis:**
- CPU is idle
- Only 1 task running
- Can handle more parallelism

**Solution:**
```json
{
  "maxParallelTasks": 6,
  "maxParallelLlmCalls": 3,
  "taskQueueSize": 20
}
```

**Result:**
```
CPU Usage: 75%
Active Tasks: 6/6
Queue Depth: 5/20
```

### Example 3: Memory High

**Symptom:**
```
Memory Usage: 12GB / 8GB (150%)
Queue Depth: 8/10
```

**Analysis:**
- Memory is exhausted
- Queue is large
- Need to reduce queue size

**Solution:**
```json
{
  "maxParallelTasks": 2,
  "maxParallelLlmCalls": 1,
  "taskQueueSize": 5
}
```

**Result:**
```
Memory Usage: 4GB / 8GB (50%)
Queue Depth: 1/5
```

### Example 4: LLM Rate Limited

**Symptom:**
```
LLM Call Errors: 429 (Too Many Requests)
Active LLM Calls: 5/5
```

**Analysis:**
- LLM provider is rate-limiting
- Too many concurrent calls
- Need to reduce concurrency

**Solution:**
```json
{
  "maxParallelTasks": 3,
  "maxParallelLlmCalls": 1,
  "taskQueueSize": 10
}
```

**Result:**
```
LLM Call Errors: 0
Active LLM Calls: 1/1
```

## Advanced Configuration

### Per-Task Limits

For future enhancement - currently not supported:
```json
{
  "tasks": {
    "expensive-task": {
      "maxParallel": 1
    },
    "cheap-task": {
      "maxParallel": 10
    }
  }
}
```

### Dynamic Limits

For future enhancement - adjust limits based on system load:
```json
{
  "dynamic": true,
  "minParallelTasks": 1,
  "maxParallelTasks": 8,
  "cpuThreshold": 0.8
}
```

### Priority Queues

For future enhancement - prioritize certain tasks:
```json
{
  "priorityQueue": true,
  "priorities": {
    "critical": 10,
    "normal": 5,
    "background": 1
  }
}
```

## Troubleshooting

### Configuration Not Applied

**Problem:** Changes to `concurrency.json` not taking effect

**Solution:**
1. Verify file syntax: `cat .aos/config/concurrency.json | jq .`
2. Restart service: `net stop GmsdService && net start GmsdService`
3. Verify new config: `aos config show concurrency`

### Queue Overflow

**Problem:** `Error: Task queue is full`

**Solution:**
1. Check queue depth: `aos metrics concurrency`
2. Increase `taskQueueSize`: `"taskQueueSize": 20`
3. Increase `maxParallelTasks`: `"maxParallelTasks": 5`
4. Restart service

### Tasks Not Executing

**Problem:** Tasks queued but not executing

**Solution:**
1. Check active tasks: `aos metrics concurrency`
2. Check for errors: `cat .aos/logs/commands.json | tail -20`
3. Check lock status: `aos lock status`
4. Verify configuration: `aos config show concurrency`

### High Memory Usage

**Problem:** Memory usage exceeds available RAM

**Solution:**
1. Check queue size: `aos metrics concurrency`
2. Reduce `maxParallelTasks`: `"maxParallelTasks": 1`
3. Reduce `taskQueueSize`: `"taskQueueSize": 5`
4. Monitor memory: `Get-Process aos | Select-Object Memory`

## Best Practices

1. **Start Conservative:** Begin with low limits, increase gradually
2. **Monitor Continuously:** Track metrics during tuning
3. **Test Thoroughly:** Verify changes with representative workloads
4. **Document Changes:** Record why limits were adjusted
5. **Plan for Growth:** Leave headroom for future workloads
6. **Review Regularly:** Adjust as workload patterns change
7. **Alert on Anomalies:** Set up alerts for queue overflow or errors

## Support

For rate limiting issues:
1. Collect metrics: `aos metrics concurrency > metrics.txt`
2. Check configuration: `aos config show concurrency > config.txt`
3. Review logs: `cat .aos/logs/commands.json > logs.txt`
4. Contact support with metrics, configuration, and logs

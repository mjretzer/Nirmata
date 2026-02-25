# Concurrency Configuration and Tuning Guide

## Overview

The GMSD orchestrator supports configurable concurrency limits to prevent resource exhaustion and ensure stable operation under load. This guide explains how to configure and tune concurrency settings for your deployment.

## Configuration File

Concurrency settings are defined in `.aos/config/concurrency.json`:

```json
{
  "maxParallelTasks": 3,
  "maxParallelLlmCalls": 2,
  "taskQueueSize": 10
}
```

## Configuration Parameters

### maxParallelTasks
- **Type:** Integer (>= 1)
- **Default:** 3
- **Description:** Maximum number of tasks that can execute in parallel
- **Tuning Guidance:**
  - Increase for CPU-bound workloads with multiple cores available
  - Decrease if memory usage is high or system is resource-constrained
  - Typical range: 2-8 depending on system resources

### maxParallelLlmCalls
- **Type:** Integer (>= 1)
- **Default:** 2
- **Description:** Maximum number of concurrent LLM provider calls
- **Tuning Guidance:**
  - Increase to improve throughput if LLM provider supports it
  - Decrease if hitting rate limits from LLM provider
  - Typical range: 1-5 depending on LLM provider quotas

### taskQueueSize
- **Type:** Integer (>= maxParallelTasks)
- **Default:** 10
- **Description:** Maximum number of tasks that can be queued waiting for execution
- **Tuning Guidance:**
  - Must be >= maxParallelTasks
  - Increase to buffer more tasks during load spikes
  - Decrease to fail fast when overloaded
  - Typical range: maxParallelTasks to 3x maxParallelTasks

## Default Configuration

If `.aos/config/concurrency.json` does not exist, the system uses these defaults:
- maxParallelTasks: 3
- maxParallelLlmCalls: 2
- taskQueueSize: 10

## Validation

The system validates concurrency configuration at startup:
- All limits must be positive integers (>= 1)
- taskQueueSize must be >= maxParallelTasks
- Invalid configurations cause startup failure with clear error messages

## Monitoring

### Metrics

Concurrency metrics are available via the `report-progress` command and include:
- Current active task count
- Current queued task count
- Current active LLM call count
- Queue depth as percentage of maximum
- Task completion rate (tasks/minute)

### Logging

The system logs concurrency metrics periodically (default: every 5 minutes) at INFO level:
```
ConcurrencyLimiter initialized: maxParallelTasks=3, maxParallelLlmCalls=2, taskQueueSize=10
Task slot acquired: taskId=task-123, activeTaskCount=1
LLM call rate limit reached: activeLlmCallCount=2, maxParallelLlmCalls=2
```

## Error Handling

### Queue Full Error
When task queue is full:
```
Task queue is full. Current queue depth: 10, max queue size: 10. Please retry after some tasks complete.
```

### LLM Rate Limit Error
When LLM call limit is reached:
```
LLM call rate limit reached. Current active calls: 2, max parallel calls: 2. Retry after 5 seconds.
```

## Tuning Examples

### High-Throughput Scenario
For systems with abundant resources and high task volume:
```json
{
  "maxParallelTasks": 8,
  "maxParallelLlmCalls": 4,
  "taskQueueSize": 24
}
```

### Resource-Constrained Scenario
For systems with limited resources:
```json
{
  "maxParallelTasks": 1,
  "maxParallelLlmCalls": 1,
  "taskQueueSize": 5
}
```

### Balanced Scenario
For typical deployments:
```json
{
  "maxParallelTasks": 3,
  "maxParallelLlmCalls": 2,
  "taskQueueSize": 10
}
```

## Restart Required

Configuration changes require service restart to take effect. The system does not support hot-reload of concurrency settings.

## Troubleshooting

### Frequent Queue Full Errors
- Increase `taskQueueSize` to buffer more tasks
- Increase `maxParallelTasks` if system resources allow
- Check if tasks are completing slowly (increase task timeout or optimize task logic)

### High LLM Call Latency
- Decrease `maxParallelLlmCalls` to reduce contention
- Check LLM provider rate limits and quotas
- Monitor LLM provider health and response times

### System Overload
- Decrease `maxParallelTasks` to reduce resource usage
- Decrease `taskQueueSize` to fail fast instead of queuing
- Monitor system CPU, memory, and network usage
- Consider scaling horizontally (multiple service instances)

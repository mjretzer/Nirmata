# nirmata Orchestrator Hardening Features Guide

This guide covers the production-grade safety features added to the nirmata orchestrator: workspace locking, run abandonment detection, pause/resume functionality, rate limiting, and secret management.

## Overview

The nirmata orchestrator now includes five major hardening features designed to make it suitable for production use:

1. **Workspace Lock Manager** - Prevents concurrent state corruption
2. **Run Abandonment Detection** - Detects and cleans up unfinished runs
3. **Pause/Resume with Status** - Explicit control over run execution
4. **Rate Limiting and Concurrency Bounds** - Resource management
5. **Secret Management** - Secure credential storage

## 1. Workspace Lock Manager

### Purpose
Prevents multiple processes from simultaneously modifying workspace state, which could cause corruption.

### How It Works
- Exclusive file-based lock at `.aos/locks/workspace.lock`
- All mutating commands acquire lock at entry
- Validation commands bypass lock requirement
- Lock contention fails fast with actionable error messages

### Usage

```bash
# Check lock status
aos lock status

# Manually acquire lock (for debugging)
aos lock acquire

# Release lock
aos lock release

# Force release (use with caution)
aos lock release --force
```

### Error Handling

If you see a lock contention error:
```
Error: Workspace is locked by another process
Lock holder: aos.exe (PID: 1234)
Lock path: C:\path\to\.aos\locks\workspace.lock
Next steps:
1. Wait for the other process to complete
2. If the process is stuck, manually release with: aos lock release --force
3. Check logs for the stuck process
```

### Configuration

No configuration needed - locking is automatic for all mutating commands.

## 2. Run Abandonment Detection and Cleanup

### Purpose
Detects runs that have been unfinished for too long (crashed, hung, etc.) and marks them as abandoned for cleanup.

### How It Works
- Configurable timeout in `.aos/config/run-lifecycle.json` (default: 24 hours)
- Background cleanup task in Windows Service marks abandoned runs
- Manual cleanup available via `aos cache prune --abandoned`
- Abandoned runs are marked, not deleted (preserves evidence)

### Configuration

Edit `.aos/config/run-lifecycle.json`:
```json
{
  "abandonmentTimeoutMinutes": 1440
}
```

### Usage

```bash
# View abandoned runs (via run index)
aos run list --status abandoned

# Manually mark a run as abandoned
aos run mark-abandoned --run-id <id>

# Manually clean abandoned runs
aos cache prune --abandoned

# Check cleanup status
aos cache status
```

### Monitoring

The background cleanup task runs periodically. Check logs for:
```
[INFO] Cleanup: Marked 3 abandoned runs
[INFO] Cleanup: Removed 2 abandoned run artifacts
```

## 3. Pause/Resume with User-Visible Status

### Purpose
Allows explicit control over run execution with clear status visibility.

### How It Works
- Run status stored in `.aos/state/run.json`: `started`, `paused`, `resumed`, `finished`, `abandoned`
- Pause/resume commands are explicit state transitions
- Status exposed via `report-progress` and UI
- Only valid transitions allowed (can't resume a finished run, etc.)

### Usage

```bash
# Pause a running run
aos run pause --run-id <id>

# Resume a paused run
aos run resume --run-id <id>

# Check run status
aos run status --run-id <id>

# View status in UI
# The run detail panel shows current status and pause/resume buttons
```

### Status Transitions

```
started → paused → started → finished
                 ↓
              abandoned
```

### Error Handling

```
# Can't pause a paused run
Error: Cannot pause run in 'paused' status. Only 'started' runs can be paused.

# Can't resume a finished run
Error: Cannot resume run in 'finished' status. Only 'paused' runs can be resumed.
```

## 4. Rate Limiting and Concurrency Bounds

### Purpose
Prevents unbounded parallel execution that could overwhelm system resources.

### How It Works
- Configurable limits in `.aos/config/concurrency.json`
- Task queue with size limit
- Separate limits for parallel tasks and LLM calls
- Enforcement at execution layer (transparent to planning)

### Configuration

Edit `.aos/config/concurrency.json`:
```json
{
  "maxParallelTasks": 3,
  "maxParallelLlmCalls": 2,
  "taskQueueSize": 10
}
```

### Defaults
- `maxParallelTasks`: 3 (concurrent task executions)
- `maxParallelLlmCalls`: 2 (concurrent LLM API calls)
- `taskQueueSize`: 10 (pending tasks in queue)

### Monitoring

Check metrics for queue depth and active tasks:
```bash
# View concurrency metrics
aos metrics concurrency

# Output:
# Queue Depth: 2/10
# Active Tasks: 3/3
# Active LLM Calls: 1/2
```

### Tuning Guidance

**Increase limits if:**
- Queue frequently fills up (backpressure)
- System has available CPU/memory
- LLM provider allows higher concurrency

**Decrease limits if:**
- System is overloaded
- LLM provider rate-limits requests
- Memory usage is high

## 5. Secret Management

See `secret-management-guide.md` for comprehensive secret management documentation.

### Quick Start

```bash
# Set an API key
aos secret set openai-api-key "sk-..."

# Use in configuration
# In .aos/config/llm-providers.json:
{
  "nirmataAgents": {
    "SemanticKernel": {
      "Provider": "OpenAi",
      "OpenAi": {
        "ApiKey": "$secret:openai-api-key"
      }
    }
  }
}

# Rotate secret
aos secret set openai-api-key "sk-new-key"
# Restart service to apply
```

## Troubleshooting

### Lock Contention

**Problem:** `Error: Workspace is locked by another process`

**Solution:**
1. Check if another `aos` process is running: `tasklist | findstr aos`
2. Wait for it to complete
3. If stuck, force release: `aos lock release --force`
4. Check logs for the stuck process

### Abandoned Runs Not Cleaning Up

**Problem:** Abandoned runs accumulate despite timeout

**Solution:**
1. Check cleanup task is running: `Get-Service nirmataService`
2. Check timeout setting: `cat .aos/config/run-lifecycle.json`
3. Manually trigger cleanup: `aos cache prune --abandoned`
4. Check service logs for errors

### Pause/Resume Not Working

**Problem:** Can't pause a run

**Solution:**
1. Check run status: `aos run status --run-id <id>`
2. Can only pause `started` runs
3. Check for lock contention: `aos lock status`
4. Review logs for state transition errors

### Rate Limiting Causing Slowdown

**Problem:** Tasks are queuing up, execution is slow

**Solution:**
1. Check queue depth: `aos metrics concurrency`
2. Increase limits in `.aos/config/concurrency.json`
3. Monitor system resources (CPU, memory)
4. Check LLM provider rate limits

### Secret Not Found

**Problem:** `Error: Secret 'openai-api-key' not found`

**Solution:**
1. List secrets: `aos secret list`
2. Check secret name matches configuration (case-sensitive)
3. Set the secret: `aos secret set openai-api-key "value"`
4. Restart service to apply

## Integration with CI/CD

### GitHub Actions Example

```yaml
- name: Set nirmata Secrets
  run: |
    aos secret set openai-api-key ${{ secrets.OPENAI_API_KEY }}
    aos secret set azure-openai-key ${{ secrets.AZURE_OPENAI_KEY }}

- name: Run nirmata
  run: aos run execute --plan-file plan.json
```

### Environment Variable Injection

```bash
# Set secrets from environment
export nirmata_SECRET_OPENAI_API_KEY="sk-..."
export nirmata_SECRET_AZURE_OPENAI_KEY="..."

# System will load from environment if available
aos run execute --plan-file plan.json
```

## Monitoring and Observability

### Metrics

The hardening features expose metrics for monitoring:

```bash
# Lock metrics
aos metrics locks
# Output: Lock acquisitions, contentions, release times

# Abandonment metrics
aos metrics abandonment
# Output: Abandoned runs, cleanup operations, cleanup duration

# Concurrency metrics
aos metrics concurrency
# Output: Queue depth, active tasks, LLM call latency
```

### Logging

All hardening operations are logged with correlation IDs:

```
[INFO] Lock acquired for 'run execute' (correlation: abc-123)
[INFO] Run marked abandoned: run-id=xyz-789 (age: 25h)
[INFO] Run paused: run-id=xyz-789 (paused-at: 2026-02-20T10:30:00Z)
[WARN] Concurrency limit reached: queue-depth=10/10
```

## Performance Impact

### Lock Manager
- Minimal overhead: ~1-5ms per lock acquisition
- No impact on validation commands (bypass lock)
- Prevents state corruption (worth the cost)

### Run Abandonment
- Background task runs periodically (configurable)
- Minimal CPU/memory impact
- Cleanup is deterministic and safe

### Pause/Resume
- Negligible overhead: status check only
- No impact on running tasks
- Improves user experience

### Rate Limiting
- Prevents resource exhaustion
- May introduce queuing delays if limits are too low
- Configurable to match system capacity

### Secret Management
- One-time cost at startup (secret resolution)
- No runtime overhead (secrets cached)
- Secure storage (OS keychain)

## Best Practices

1. **Locking:** Don't manually release locks unless necessary
2. **Abandonment:** Set timeout based on your workload patterns
3. **Pause/Resume:** Use for long-running sessions that need breaks
4. **Rate Limiting:** Start with defaults, tune based on metrics
5. **Secrets:** Rotate regularly, never commit plaintext keys
6. **Monitoring:** Enable metrics and review logs regularly

## Support and Questions

For issues or questions:
1. Check the troubleshooting section above
2. Review application logs with correlation IDs
3. Contact the nirmata team with:
   - Error message (without secret values)
   - Configuration structure (without secret values)
   - Steps to reproduce
   - Relevant logs

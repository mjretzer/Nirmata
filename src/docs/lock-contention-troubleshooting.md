# Lock Contention Troubleshooting Guide

## Overview

Lock contention occurs when multiple processes try to access the workspace simultaneously. This guide helps diagnose and resolve lock-related issues.

## Common Scenarios

### Scenario 1: "Workspace is locked" Error

**Symptom:**
```
Error: Workspace is locked by another process
Lock holder: aos.exe (PID: 1234)
Lock path: C:\path\to\.aos\locks\workspace.lock
```

**Diagnosis:**
1. Check if the lock holder process is still running:
   ```bash
   tasklist | findstr aos
   ```

2. Check lock status:
   ```bash
   aos lock status
   ```

3. Review logs for the lock holder:
   ```bash
   tail -f .aos/logs/commands.json
   ```

**Resolution:**

**If the process is still running:**
- Wait for it to complete
- Check progress: `aos run status --run-id <id>`
- Monitor CPU/memory: `Get-Process aos | Select-Object Name, CPU, Memory`

**If the process is stuck:**
1. Force release the lock:
   ```bash
   aos lock release --force
   ```

2. Verify lock is released:
   ```bash
   aos lock status
   ```

3. Investigate why the process got stuck:
   - Check service logs: `Get-EventLog -LogName Application -Source nirmataService`
   - Check application logs: `cat .aos/logs/commands.json | tail -20`
   - Look for errors or timeouts

**If the process crashed:**
1. The lock file may be orphaned
2. Force release:
   ```bash
   aos lock release --force
   ```

3. Restart the service:
   ```bash
   net stop nirmataService
   net start nirmataService
   ```

### Scenario 2: Frequent Lock Contention

**Symptom:**
- Multiple commands fail with lock contention
- Logs show repeated lock acquisition attempts
- Performance is degraded

**Diagnosis:**
1. Check for long-running operations:
   ```bash
   aos lock status --verbose
   ```

2. Review command log for slow operations:
   ```bash
   cat .aos/logs/commands.json | jq '.[] | select(.duration > 30000)'
   ```

3. Check system resources:
   ```bash
   Get-Process aos | Select-Object Name, CPU, Memory, Handles
   ```

**Resolution:**

**Optimize long-running operations:**
- Reduce task complexity
- Increase rate limiting thresholds
- Split large operations into smaller ones

**Increase lock timeout:**
Edit `.aos/config/locks.json`:
```json
{
  "lockTimeoutSeconds": 30,
  "lockRetryIntervalMs": 100,
  "maxLockRetries": 300
}
```

**Reduce concurrent operations:**
- Don't run multiple `aos` commands simultaneously
- Use task scheduling to serialize operations
- Implement operation queuing at application level

### Scenario 3: Deadlock Situation

**Symptom:**
- Multiple processes waiting for lock
- None can acquire lock
- System appears hung

**Diagnosis:**
1. Check all lock holders:
   ```bash
   Get-Process aos -ErrorAction SilentlyContinue | ForEach-Object { 
     Write-Host "PID: $($_.Id), Name: $($_.ProcessName), CPU: $($_.CPU)" 
   }
   ```

2. Check lock file timestamp:
   ```bash
   Get-Item .aos/locks/workspace.lock | Select-Object LastWriteTime
   ```

3. Review logs for circular dependencies

**Resolution:**

1. Force release all locks:
   ```bash
   aos lock release --force
   ```

2. Kill stuck processes:
   ```bash
   Get-Process aos | Stop-Process -Force
   ```

3. Restart service:
   ```bash
   net stop nirmataService
   net start nirmataService
   ```

4. Investigate root cause:
   - Check for infinite loops in code
   - Review task dependencies
   - Look for circular wait conditions

## Prevention Strategies

### 1. Implement Exponential Backoff

When lock contention occurs, retry with increasing delays:
```csharp
int maxRetries = 5;
int delayMs = 100;

for (int i = 0; i < maxRetries; i++)
{
    try
    {
        lockManager.Acquire();
        break;
    }
    catch (LockContendedException)
    {
        if (i < maxRetries - 1)
        {
            await Task.Delay(delayMs);
            delayMs *= 2; // Exponential backoff
        }
    }
}
```

### 2. Use Validation Commands

Validation commands bypass locks, so use them for read-only operations:
```bash
# This acquires lock
aos run execute --plan-file plan.json

# This does NOT acquire lock
aos validate --workspace-root .
```

### 3. Serialize Operations

Use task scheduling to avoid concurrent operations:
```powershell
# Schedule operations sequentially
$tasks = @(
    { aos run execute --plan-file plan1.json },
    { aos run execute --plan-file plan2.json },
    { aos run execute --plan-file plan3.json }
)

foreach ($task in $tasks) {
    & $task
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Task failed"
        break
    }
}
```

### 4. Monitor Lock Metrics

Enable lock metrics to track contention:
```bash
# View lock metrics
aos metrics locks

# Output:
# Total acquisitions: 1234
# Total contentions: 12
# Average wait time: 150ms
# Max wait time: 2500ms
```

### 5. Set Appropriate Timeouts

Configure lock timeouts based on expected operation duration:
```json
{
  "lockTimeoutSeconds": 60,
  "lockRetryIntervalMs": 100,
  "maxLockRetries": 600
}
```

## Advanced Debugging

### Enable Lock Tracing

Set environment variable for detailed lock logging:
```bash
set nirmata_LOCK_TRACE=1
aos run execute --plan-file plan.json
```

This produces detailed logs:
```
[TRACE] Lock acquisition requested: command=run execute
[TRACE] Lock file path: C:\path\to\.aos\locks\workspace.lock
[TRACE] Lock holder: aos.exe (PID: 1234)
[TRACE] Waiting for lock release...
[TRACE] Lock acquired after 1500ms
```

### Inspect Lock File

The lock file contains metadata about the lock holder:
```bash
cat .aos/locks/workspace.lock | jq .
```

Output:
```json
{
  "processId": 1234,
  "processName": "aos.exe",
  "command": "run execute",
  "acquiredAt": "2026-02-20T10:30:00Z",
  "expiresAt": "2026-02-20T10:31:00Z"
}
```

### Review Lock History

Lock operations are logged in commands.json:
```bash
cat .aos/logs/commands.json | jq '.[] | select(.command == "lock")'
```

## Recovery Procedures

### Complete Lock Reset

If locks are in an inconsistent state:

1. Stop the service:
   ```bash
   net stop nirmataService
   ```

2. Remove lock files:
   ```bash
   Remove-Item .aos/locks/* -Force
   ```

3. Restart the service:
   ```bash
   net start nirmataService
   ```

4. Verify workspace is valid:
   ```bash
   aos validate --workspace-root .
   ```

### Backup and Restore

If lock issues corrupt state:

1. Backup current state:
   ```bash
   Copy-Item .aos .aos.backup -Recurse
   ```

2. Reset locks:
   ```bash
   Remove-Item .aos/locks/* -Force
   ```

3. Verify state:
   ```bash
   aos validate --workspace-root .
   ```

4. If validation fails, restore from backup:
   ```bash
   Remove-Item .aos -Recurse
   Copy-Item .aos.backup .aos -Recurse
   ```

## Performance Tuning

### Reduce Lock Contention

**Increase lock timeout:**
```json
{
  "lockTimeoutSeconds": 120
}
```

**Increase retry interval:**
```json
{
  "lockRetryIntervalMs": 500
}
```

**Implement read-write locks:**
- Use validation commands for read-only operations
- Only acquire locks for mutations

### Monitor and Alert

Set up monitoring for lock contention:
```powershell
# Alert if contention exceeds threshold
$contentions = (aos metrics locks | jq '.contentions')
if ($contentions -gt 10) {
    Send-Alert "High lock contention: $contentions"
}
```

## Support

For persistent lock issues:

1. Collect diagnostics:
   ```bash
   aos lock status --verbose > lock-status.txt
   Get-Process aos > processes.txt
   cat .aos/logs/commands.json > commands.txt
   ```

2. Contact support with:
   - Lock status output
   - Process list
   - Command logs
   - Error messages
   - Steps to reproduce

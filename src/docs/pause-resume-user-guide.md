# Pause/Resume User Guide

## Overview

The pause/resume feature allows you to pause long-running orchestration sessions and resume them later. This is useful for:
- Taking breaks during long sessions
- Investigating intermediate results
- Handling interruptions gracefully
- Managing resource usage

## Basic Usage

### Pausing a Run

```bash
# Pause a running orchestration
aos run pause --run-id <run-id>

# Output:
# Run '<run-id>' paused successfully
```

### Resuming a Run

```bash
# Resume a paused orchestration
aos run resume --run-id <run-id>

# Output:
# Run '<run-id>' resumed successfully
```

### Checking Run Status

```bash
# Check current status
aos run status --run-id <run-id>

# Output:
# Run ID: run-abc-123
# Status: paused
# Started: 2026-02-20 10:30:00 UTC
# Paused: 2026-02-20 11:15:00 UTC
# Duration: 45 minutes
```

## UI Integration

### Status Display

The run detail panel shows the current status:
- **Running** - Normal execution
- **Paused** - Waiting to resume
- **Finished** - Completed successfully
- **Abandoned** - Exceeded timeout

### Pause/Resume Buttons

The UI provides buttons to control pause/resume:
- **Pause** button appears when status is "running"
- **Resume** button appears when status is "paused"
- Buttons are disabled for finished or abandoned runs

### Status History

View pause/resume history in the run details:
```
Timeline:
- 10:30:00 - Run started
- 11:15:00 - Run paused (45 min elapsed)
- 11:20:00 - Run resumed
- 12:00:00 - Run finished (50 min total)
```

## State Transitions

### Valid Transitions

```
started → paused → started → finished
                 ↓
              abandoned (if timeout exceeded)
```

### Invalid Transitions

You cannot:
- Pause a paused run
- Resume a finished run
- Resume an abandoned run
- Pause a finished run

Attempting invalid transitions returns an error:
```
Error: Cannot pause run in 'paused' status. Only 'started' runs can be paused.
```

## Use Cases

### Case 1: Investigation Break

```bash
# Run is executing tasks
# You want to check intermediate results

# Pause the run
aos run pause --run-id run-abc-123

# Investigate results
cat .aos/evidence/runs/run-abc-123/result.json | jq .

# Review logs
cat .aos/evidence/runs/run-abc-123/commands.json | tail -20

# Resume when ready
aos run resume --run-id run-abc-123
```

### Case 2: Resource Management

```bash
# System is under load
# Pause orchestration to free resources

aos run pause --run-id run-abc-123

# Wait for system to recover
# Monitor resource usage
Get-Process aos | Select-Object Memory, CPU

# Resume when ready
aos run resume --run-id run-abc-123
```

### Case 3: Long-Running Session

```bash
# Orchestration will take 8 hours
# You want to pause for lunch

# Start execution
aos run execute --plan-file plan.json

# After 4 hours, pause
aos run pause --run-id run-abc-123

# Take a break, investigate results
# Resume after lunch
aos run resume --run-id run-abc-123

# Wait for completion
```

### Case 4: Handling Interruptions

```bash
# Unexpected issue detected
# Pause to investigate

aos run pause --run-id run-abc-123

# Investigate issue
# Fix configuration if needed
# Update secrets if needed

# Resume execution
aos run resume --run-id run-abc-123
```

## Behavior During Pause

When a run is paused:

1. **Current task completes** - In-flight tasks are allowed to finish
2. **No new tasks start** - Queued tasks wait for resume
3. **State is preserved** - All intermediate results are saved
4. **Logs are updated** - Pause event is logged with timestamp
5. **Resources are released** - LLM calls and task execution stop

### Example Timeline

```
10:30:00 - Run started
10:35:00 - Task 1 completes
10:40:00 - Task 2 in progress
10:45:00 - Pause requested
10:45:15 - Task 2 completes (in-flight task)
10:45:16 - Run paused (Task 3 not started)
11:20:00 - Resume requested
11:20:01 - Task 3 starts
11:25:00 - Task 3 completes
11:30:00 - Run finished
```

## Behavior During Resume

When a run is resumed:

1. **State is restored** - Previous execution state is loaded
2. **Execution continues** - Next queued task starts
3. **Logs are updated** - Resume event is logged with timestamp
4. **No re-execution** - Completed tasks are not re-run
5. **Resources are acquired** - Lock is re-acquired if needed

## Monitoring Pause/Resume

### View Pause/Resume History

```bash
# Get run metadata
cat .aos/evidence/runs/run-abc-123/run.json | jq '.pauseResumeHistory'

# Output:
# [
#   {
#     "action": "paused",
#     "timestamp": "2026-02-20T11:15:00Z",
#     "duration": 45
#   },
#   {
#     "action": "resumed",
#     "timestamp": "2026-02-20T11:20:00Z"
#   }
# ]
```

### Track Total Pause Time

```bash
# Calculate total pause duration
cat .aos/evidence/runs/run-abc-123/run.json | 
  jq '[.pauseResumeHistory[] | select(.action == "paused") | .duration] | add'

# Output: 45 (minutes)
```

### Monitor Pause Events

```bash
# View pause/resume in command log
cat .aos/evidence/runs/run-abc-123/commands.json | 
  jq '.[] | select(.command | contains("pause") or contains("resume"))'
```

## Best Practices

### 1. Pause at Safe Points

- Pause after task completion, not during task execution
- Avoid pausing during critical operations
- Check task status before pausing

```bash
# Check current task
aos run status --run-id <id> --verbose

# Pause after current task completes
aos run pause --run-id <id>
```

### 2. Document Pause Reasons

When pausing, document why:
```bash
# Add note to run metadata
aos run annotate --run-id <id> --note "Paused for investigation of Task 5 results"
```

### 3. Set Expectations

For long pauses, set a resume time:
```bash
# Pause with expected resume time
aos run pause --run-id <id> --resume-at "2026-02-20 14:00:00"

# System can alert if resume time is missed
```

### 4. Monitor Pause Duration

Long pauses may indicate issues:
```bash
# Alert if pause exceeds threshold
$pauseDuration = (Get-Date) - (Get-Item .aos/evidence/runs/<id>/run.json).LastWriteTime
if ($pauseDuration.TotalHours -gt 4) {
    Send-Alert "Run paused for more than 4 hours"
}
```

### 5. Plan for Resume

Before resuming, verify:
- System resources are available
- Configuration is still valid
- Secrets haven't expired
- Network connectivity is good

```bash
# Pre-resume checks
aos validate --workspace-root .
aos secret list | grep -q openai-api-key
Test-NetConnection -ComputerName api.openai.com -Port 443

# Then resume
aos run resume --run-id <id>
```

## Troubleshooting

### Can't Pause Run

**Error:** `Error: Cannot pause run in 'finished' status`

**Solution:**
- Only running runs can be paused
- Check run status: `aos run status --run-id <id>`
- If finished, start a new run

### Can't Resume Run

**Error:** `Error: Cannot resume run in 'finished' status`

**Solution:**
- Only paused runs can be resumed
- Check run status: `aos run status --run-id <id>`
- If finished, start a new run

### Run Abandoned While Paused

**Problem:** Run was paused for too long and marked as abandoned

**Solution:**
1. Adjust abandonment timeout: `.aos/config/run-lifecycle.json`
2. Resume before timeout expires
3. Investigate why pause was so long

```bash
# Check timeout
cat .aos/config/run-lifecycle.json | jq '.abandonmentTimeoutMinutes'

# Resume before timeout
aos run resume --run-id <id>
```

### Resume Fails

**Error:** `Error: Failed to resume run - state corrupted`

**Solution:**
1. Check run metadata: `cat .aos/evidence/runs/<id>/run.json`
2. Verify workspace is valid: `aos validate --workspace-root .`
3. Check for lock contention: `aos lock status`
4. Restore from backup if needed

## Integration with Automation

### Pause on Error

Automatically pause on error for investigation:
```csharp
try
{
    await taskExecutor.ExecuteAsync(task);
}
catch (Exception ex)
{
    logger.LogError($"Task failed: {ex.Message}");
    runManager.PauseRun(runId);
    // Operator can investigate and resume
}
```

### Resume on Schedule

Resume paused runs on a schedule:
```powershell
# Resume all paused runs at 8 AM
$trigger = New-JobTrigger -Daily -At "08:00:00"
Register-ScheduledJob -Name "ResumeRuns" -ScriptBlock {
    aos run list --status paused | ForEach-Object {
        aos run resume --run-id $_.RunId
    }
} -Trigger $trigger
```

### Pause on Resource Threshold

Pause when resources exceed threshold:
```powershell
while ($true) {
    $memory = (Get-Process aos | Measure-Object -Property Memory -Sum).Sum / 1GB
    if ($memory -gt 8) {
        aos run pause --run-id $runId
        Write-Host "Paused due to high memory: $memory GB"
    }
    Start-Sleep -Seconds 60
}
```

## Performance Considerations

### Pause Overhead
- Minimal: ~100ms to pause
- Completes in-flight tasks
- No state corruption risk

### Resume Overhead
- Minimal: ~100ms to resume
- State restoration is fast
- No re-execution of completed tasks

### Long Pause Impact
- No resource consumption while paused
- Workspace lock is released
- Other operations can proceed
- Run evidence is preserved

## Support

For pause/resume issues:
1. Check run status: `aos run status --run-id <id>`
2. Review run metadata: `cat .aos/evidence/runs/<id>/run.json`
3. Check logs: `cat .aos/logs/commands.json`
4. Contact support with run ID and status

# Abandoned Runs Detection and Cleanup Guide

## Overview

Abandoned runs are unfinished runs that have exceeded the configured timeout period. They indicate runs that crashed, hung, or were otherwise interrupted. This guide explains how to detect, manage, and clean up abandoned runs.

## How Abandonment Works

### Detection

The system detects abandoned runs by comparing:
- **Run start time** (`startedAtUtc` in run.json)
- **Current time**
- **Abandonment timeout** (configurable, default: 24 hours)

A run is considered abandoned if:
```
currentTime - startedAtUtc > abandonmentTimeoutMinutes
AND run.status != "finished"
AND run.status != "abandoned"
```

### Marking as Abandoned

When a run is detected as abandoned:
1. Run status is changed to `abandoned`
2. Run is added to abandoned runs index
3. Evidence is preserved for investigation
4. Cleanup can be performed manually or automatically

### Cleanup

Cleanup removes:
- Run metadata files
- Run result files
- Run evidence (logs, calls, etc.)
- Run index entries

**Note:** Cleanup is optional and can be deferred for investigation.

## Configuration

### Setting Abandonment Timeout

Edit `.aos/config/run-lifecycle.json`:

```json
{
  "abandonmentTimeoutMinutes": 1440
}
```

**Recommended values:**
- **Development:** 60 minutes (1 hour)
- **Staging:** 360 minutes (6 hours)
- **Production:** 1440 minutes (24 hours)

### Disabling Abandonment Detection

Set timeout to a very large value:
```json
{
  "abandonmentTimeoutMinutes": 525600
}
```

This effectively disables abandonment (1 year timeout).

## Usage

### View Abandoned Runs

```bash
# List all abandoned runs
aos run list --status abandoned

# Output:
# Run ID                Status      Started At          Age
# run-abc-123          abandoned   2026-02-19 10:30    15h 45m
# run-def-456          abandoned   2026-02-18 14:20    39h 30m
```

### Check Specific Run Status

```bash
aos run status --run-id run-abc-123

# Output:
# Run ID: run-abc-123
# Status: abandoned
# Started: 2026-02-19 10:30:00 UTC
# Age: 15 hours 45 minutes
# Evidence: Available for investigation
```

### Manually Mark Run as Abandoned

```bash
aos run mark-abandoned --run-id run-abc-123

# Output:
# Run 'run-abc-123' marked as abandoned
```

### Clean Abandoned Runs

```bash
# Clean all abandoned runs
aos cache prune --abandoned

# Output:
# Cleaning abandoned runs...
# Marked abandoned: 2 runs
# Cleaned up: 2 runs
# Freed space: 245 MB
```

### Clean Specific Run

```bash
aos cache prune --abandoned --run-id run-abc-123

# Output:
# Cleaned run 'run-abc-123'
# Freed space: 120 MB
```

## Investigation

### Before Cleanup

Always investigate abandoned runs before cleanup to understand why they were abandoned.

### Examine Run Evidence

```bash
# View run metadata
cat .aos/evidence/runs/run-abc-123/run.json | jq .

# View run result (if available)
cat .aos/evidence/runs/run-abc-123/result.json | jq .

# View command log
cat .aos/evidence/runs/run-abc-123/commands.json | jq '.[-5:]'

# View LLM calls
cat .aos/evidence/calls/run-abc-123/calls.json | jq '.[-5:]'
```

### Check System Logs

```bash
# View Windows Event Log
Get-EventLog -LogName Application -Source GmsdService | 
  Where-Object { $_.TimeGenerated -gt (Get-Date).AddHours(-24) } |
  Select-Object TimeGenerated, Message

# View application logs
cat .aos/logs/commands.json | jq '.[] | select(.runId == "run-abc-123")'
```

### Analyze Failure Patterns

```bash
# Find all abandoned runs from today
cat .aos/evidence/runs/index.json | jq '.[] | select(.status == "abandoned" and .startedAtUtc > "2026-02-20")'

# Count by hour
cat .aos/evidence/runs/index.json | jq '.[] | select(.status == "abandoned") | .startedAtUtc' | 
  cut -d'T' -f2 | cut -d':' -f1 | sort | uniq -c
```

## Common Causes

### 1. Process Crash

**Symptoms:**
- Run status is `started`
- No recent activity in logs
- Process not running

**Investigation:**
```bash
# Check Windows Event Log for crash
Get-EventLog -LogName Application -Source GmsdService | 
  Where-Object { $_.Message -match "crash|exception" }
```

**Prevention:**
- Enable crash dumps: `set GMSD_DUMP_ON_CRASH=1`
- Monitor process health
- Set up alerts for process termination

### 2. Deadlock or Hang

**Symptoms:**
- Run status is `started`
- Last activity was hours ago
- Process still running but not progressing

**Investigation:**
```bash
# Check if process is hung
Get-Process aos | Select-Object Name, Handles, Memory, CPU

# Check for lock contention
aos lock status

# Review logs for stuck operations
cat .aos/logs/commands.json | tail -20
```

**Prevention:**
- Set operation timeouts
- Implement watchdog timers
- Monitor queue depth

### 3. Resource Exhaustion

**Symptoms:**
- Run status is `started`
- High memory/CPU usage
- Process becomes unresponsive

**Investigation:**
```bash
# Check system resources
Get-Process aos | Select-Object Name, Memory, CPU, Handles

# Check disk space
Get-Volume | Select-Object DriveLetter, Size, SizeRemaining

# Review concurrency metrics
aos metrics concurrency
```

**Prevention:**
- Adjust rate limiting thresholds
- Monitor resource usage
- Implement circuit breakers

### 4. Network Timeout

**Symptoms:**
- Run status is `started`
- Last activity was during LLM call
- Network connectivity issues

**Investigation:**
```bash
# Check network connectivity
Test-NetConnection -ComputerName api.openai.com -Port 443

# Review LLM call logs
cat .aos/evidence/calls/run-abc-123/calls.json | jq '.[] | select(.status == "timeout")'
```

**Prevention:**
- Set appropriate timeouts
- Implement retry logic
- Monitor network connectivity

## Automation

### Automatic Cleanup

Enable automatic cleanup in Windows Service:

Edit service configuration:
```json
{
  "cleanupSchedule": "0 2 * * *",
  "cleanupAbandonedRuns": true,
  "retentionDays": 7
}
```

This cleans abandoned runs older than 7 days at 2 AM daily.

### Scheduled Cleanup Script

```powershell
# Schedule cleanup every 6 hours
$trigger = New-JobTrigger -RepeatIndefinitely -At (Get-Date) -RepetitionInterval (New-TimeSpan -Hours 6)
Register-ScheduledJob -Name "CleanAbandonedRuns" -ScriptBlock {
    aos cache prune --abandoned
} -Trigger $trigger
```

### Monitoring and Alerts

```powershell
# Alert if too many abandoned runs
$abandoned = (aos run list --status abandoned | Measure-Object).Count
if ($abandoned -gt 10) {
    Send-Alert "Too many abandoned runs: $abandoned"
}

# Alert if cleanup fails
$result = aos cache prune --abandoned
if ($LASTEXITCODE -ne 0) {
    Send-Alert "Cleanup failed: $result"
}
```

## Best Practices

### 1. Regular Investigation

- Review abandoned runs weekly
- Investigate patterns and root causes
- Update timeout based on findings

### 2. Preserve Evidence

- Don't immediately clean abandoned runs
- Keep evidence for at least 7 days
- Archive evidence for long-term analysis

### 3. Monitor Trends

```bash
# Track abandonment rate
cat .aos/evidence/runs/index.json | 
  jq '[.[] | select(.status == "abandoned")] | length'

# Alert if rate exceeds threshold
if (abandonmentCount > 5 per day) {
    Investigate root cause
}
```

### 4. Adjust Timeout

- Start with default (24 hours)
- Monitor actual run durations
- Adjust based on workload patterns

```bash
# Find longest running completed runs
cat .aos/evidence/runs/index.json | 
  jq '.[] | select(.status == "finished") | .duration' | 
  sort -rn | head -10
```

### 5. Implement Heartbeat

For long-running operations, implement heartbeat:
```csharp
// Update run status periodically
while (running)
{
    // Do work
    await Task.Delay(TimeSpan.FromMinutes(5));
    
    // Update heartbeat
    runManager.UpdateHeartbeat(runId);
}
```

## Troubleshooting

### Cleanup Fails

**Error:** `Error: Cannot clean run - evidence locked`

**Solution:**
1. Check if run is still in progress: `aos run status --run-id <id>`
2. Wait for run to complete
3. Force cleanup: `aos cache prune --abandoned --force`

### Abandoned Runs Not Detected

**Problem:** Runs older than timeout not marked as abandoned

**Solution:**
1. Check timeout setting: `cat .aos/config/run-lifecycle.json`
2. Check background task is running: `Get-Service GmsdService`
3. Manually mark: `aos run mark-abandoned --run-id <id>`

### Cleanup Takes Too Long

**Problem:** Cleanup operation is slow

**Solution:**
1. Check disk I/O: `Get-Disk | Select-Object Number, HealthStatus`
2. Clean in batches: `aos cache prune --abandoned --batch-size 5`
3. Run during off-hours

## Recovery

### Restore Abandoned Run

If a run was cleaned but needs to be restored:

1. Check backup: `ls .aos.backup/evidence/runs/run-abc-123/`
2. Restore from backup: `cp -r .aos.backup/evidence/runs/run-abc-123 .aos/evidence/runs/`
3. Update index: `aos run list --refresh-index`

### Investigate Cleaned Run

If evidence was cleaned but investigation is needed:

1. Check Windows Event Log for crash dumps
2. Check application logs for error messages
3. Review metrics for resource issues
4. Check network logs for connectivity issues

## Support

For abandoned run issues, provide:
- Run ID and status
- Timeout configuration
- Run age and duration
- System resources at time of abandonment
- Relevant logs and evidence

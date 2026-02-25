# Migration Guide: Hardening Features

This guide helps you migrate existing GMSD deployments to use the new hardening features.

## Overview

The hardening features are designed to be backward compatible. Existing deployments can adopt them incrementally without breaking changes.

## Migration Path

### Phase 1: Preparation (No Downtime)

1. **Backup Current State**
   ```bash
   Copy-Item .aos .aos.backup -Recurse
   ```

2. **Review Current Configuration**
   ```bash
   cat .aos/config/llm-providers.json
   cat .aos/config/run-lifecycle.json
   ```

3. **Plan Secret Migration**
   - Identify all plaintext API keys
   - Plan secret naming convention
   - Prepare Windows Credential Manager

### Phase 2: Enable Lock Manager (Recommended First)

1. **Update Service Configuration**
   ```json
   {
     "features": {
       "lockManager": true
     }
   }
   ```

2. **Test Lock Functionality**
   ```bash
   aos lock status
   aos lock acquire
   aos lock release
   ```

3. **Verify All Commands Work**
   ```bash
   aos run execute --plan-file test.json
   aos validate --workspace-root .
   ```

### Phase 3: Enable Run Abandonment Detection

1. **Create Run Lifecycle Configuration**
   ```bash
   cat > .aos/config/run-lifecycle.json << 'EOF'
   {
     "abandonmentTimeoutMinutes": 1440
   }
   EOF
   ```

2. **Enable Background Cleanup Task**
   ```json
   {
     "features": {
       "abandonmentDetection": true,
       "backgroundCleanup": true
     }
   }
   ```

3. **Monitor for Abandoned Runs**
   ```bash
   aos run list --status abandoned
   ```

### Phase 4: Enable Pause/Resume

1. **Update Service Configuration**
   ```json
   {
     "features": {
       "pauseResume": true
     }
   }
   ```

2. **Test Pause/Resume**
   ```bash
   aos run execute --plan-file test.json &
   aos run pause --run-id <id>
   aos run resume --run-id <id>
   ```

3. **Update UI** (if using custom UI)
   - Add pause/resume buttons
   - Display run status
   - Handle state transitions

### Phase 5: Enable Rate Limiting

1. **Create Concurrency Configuration**
   ```bash
   cat > .aos/config/concurrency.json << 'EOF'
   {
     "maxParallelTasks": 3,
     "maxParallelLlmCalls": 2,
     "taskQueueSize": 10
   }
   EOF
   ```

2. **Enable Rate Limiting**
   ```json
   {
     "features": {
       "rateLimiting": true
     }
   }
   ```

3. **Monitor Metrics**
   ```bash
   aos metrics concurrency
   ```

4. **Tune Configuration**
   - Monitor for 24 hours
   - Adjust limits based on metrics
   - Document final configuration

### Phase 6: Migrate Secrets (Final Phase)

1. **Identify Plaintext Secrets**
   ```bash
   grep -r "apiKey\|api_key\|token\|secret" .aos/config/
   ```

2. **Migrate to Credential Manager**
   ```bash
   # For each plaintext secret:
   aos secret set openai-api-key "sk-..."
   aos secret set azure-openai-key "..."
   ```

3. **Update Configuration**
   ```json
   {
     "GmsdAgents": {
       "SemanticKernel": {
         "Provider": "OpenAi",
         "OpenAi": {
           "ApiKey": "$secret:openai-api-key"
         }
       }
     }
   }
   ```

4. **Verify Secrets Work**
   ```bash
   aos run execute --plan-file test.json
   ```

5. **Remove Plaintext Secrets**
   ```bash
   # After verification, remove plaintext values from config files
   ```

## Rollback Procedures

### If Issues Occur

1. **Stop Service**
   ```bash
   net stop GmsdService
   ```

2. **Restore Backup**
   ```bash
   Remove-Item .aos -Recurse
   Copy-Item .aos.backup .aos -Recurse
   ```

3. **Restart Service**
   ```bash
   net start GmsdService
   ```

4. **Verify Functionality**
   ```bash
   aos validate --workspace-root .
   ```

## Feature Flags

Control feature adoption with feature flags:

```json
{
  "features": {
    "lockManager": true,
    "abandonmentDetection": true,
    "pauseResume": true,
    "rateLimiting": true,
    "secretManagement": true
  }
}
```

## Configuration Files

### Lock Manager
- **File:** `.aos/config/locks.json` (optional)
- **Default:** File-based locks at `.aos/locks/workspace.lock`

### Run Abandonment
- **File:** `.aos/config/run-lifecycle.json`
- **Required:** `abandonmentTimeoutMinutes`

### Pause/Resume
- **File:** `.aos/state/run.json`
- **Status field:** `started`, `paused`, `finished`, `abandoned`

### Rate Limiting
- **File:** `.aos/config/concurrency.json`
- **Required:** `maxParallelTasks`, `maxParallelLlmCalls`, `taskQueueSize`

### Secret Management
- **File:** `.aos/config/llm-providers.json`
- **Syntax:** `"$secret:secret-name"`

## Data Migration

### Existing Runs

Existing runs continue to work with new features:
- Lock manager applies to new operations
- Abandonment detection applies to unfinished runs
- Pause/resume available for running runs
- Rate limiting applies to new tasks

### Existing Configuration

Existing configuration is compatible:
- Plaintext secrets continue to work (during transition)
- Lock manager is transparent
- Abandonment detection is automatic
- Pause/resume is opt-in

## Testing Checklist

Before production deployment:

- [ ] Lock manager works with all commands
- [ ] Abandonment detection marks old runs correctly
- [ ] Pause/resume state transitions work
- [ ] Rate limiting enforces limits
- [ ] Secrets are stored securely
- [ ] All existing functionality still works
- [ ] Performance is acceptable
- [ ] Monitoring and metrics work
- [ ] Logs are clear and helpful
- [ ] Error messages are actionable

## Deployment Checklist

Before deploying to production:

- [ ] Backup current state
- [ ] Review all configuration files
- [ ] Test each feature individually
- [ ] Test features together
- [ ] Monitor for 24 hours in staging
- [ ] Document any custom configurations
- [ ] Train operations team
- [ ] Set up monitoring and alerts
- [ ] Plan rollback procedure
- [ ] Schedule deployment during low-traffic window

## Monitoring Setup

### Metrics to Monitor

```bash
# Lock contention
aos metrics locks | jq '.contentions'

# Abandoned runs
aos run list --status abandoned | wc -l

# Queue depth
aos metrics concurrency | jq '.queueDepth'

# Secret access
cat .aos/logs/commands.json | grep secret | wc -l
```

### Alerts to Configure

1. **Lock Contention Alert**
   - Trigger: More than 5 contentions per hour
   - Action: Investigate long-running operations

2. **Abandoned Runs Alert**
   - Trigger: More than 3 abandoned runs per day
   - Action: Investigate crash patterns

3. **Queue Overflow Alert**
   - Trigger: Queue depth exceeds 80%
   - Action: Increase `maxParallelTasks`

4. **Secret Access Alert**
   - Trigger: Failed secret access
   - Action: Verify secret exists and is accessible

## Troubleshooting Migration

### Lock Manager Issues

**Problem:** Commands fail with lock error

**Solution:**
1. Check lock status: `aos lock status`
2. Force release if stuck: `aos lock release --force`
3. Verify lock file permissions
4. Check service account permissions

### Abandonment Detection Issues

**Problem:** Runs not marked as abandoned

**Solution:**
1. Check timeout: `cat .aos/config/run-lifecycle.json`
2. Verify background task is running
3. Check service logs for errors
4. Manually mark: `aos run mark-abandoned --run-id <id>`

### Pause/Resume Issues

**Problem:** Can't pause/resume runs

**Solution:**
1. Check run status: `aos run status --run-id <id>`
2. Verify feature is enabled
3. Check for lock contention
4. Review logs for errors

### Rate Limiting Issues

**Problem:** Tasks are queued but not executing

**Solution:**
1. Check configuration: `cat .aos/config/concurrency.json`
2. Verify limits are reasonable
3. Check for errors in logs
4. Monitor queue depth: `aos metrics concurrency`

### Secret Migration Issues

**Problem:** Secrets not found after migration

**Solution:**
1. Verify secret exists: `aos secret list`
2. Check secret name matches configuration
3. Verify Windows Credential Manager access
4. Check service account permissions

## Performance Expectations

### Lock Manager
- Overhead: ~1-5ms per lock acquisition
- Impact: Minimal, prevents corruption

### Abandonment Detection
- Overhead: Background task runs periodically
- Impact: Minimal CPU/memory

### Pause/Resume
- Overhead: ~100ms per pause/resume
- Impact: Negligible

### Rate Limiting
- Overhead: Queue management
- Impact: May introduce delays if limits are too low

### Secret Management
- Overhead: One-time at startup
- Impact: Negligible after startup

## Post-Migration

### Validation

1. **Verify All Features Work**
   ```bash
   aos lock status
   aos run list --status abandoned
   aos run pause --run-id <test-id>
   aos metrics concurrency
   aos secret list
   ```

2. **Check Logs**
   ```bash
   cat .aos/logs/commands.json | tail -50
   ```

3. **Monitor Metrics**
   ```bash
   aos metrics locks
   aos metrics concurrency
   ```

### Documentation

1. **Update Runbooks**
   - Add lock contention procedures
   - Add abandonment cleanup procedures
   - Add pause/resume procedures

2. **Train Team**
   - Lock manager basics
   - Troubleshooting procedures
   - Monitoring and alerts

3. **Document Configuration**
   - Record final configuration
   - Document any customizations
   - Document tuning decisions

## Support

For migration assistance:
1. Review this guide completely
2. Test each feature in staging
3. Monitor during rollout
4. Contact support if issues arise

Provide:
- Current configuration
- Error messages
- Logs from migration
- Steps taken so far

# Migration Guide: Intent Classification False Positives Fix

## Breaking Change Summary

The input classifier has been changed from keyword-based detection to explicit command prefix syntax. This is a **breaking change** that affects how users interact with the system.

## What Changed

### Before (Old Behavior)
- Freeform text like "create a plan", "run the tests", "fix the bug" would trigger workflow actions
- Keywords were matched anywhere in the input using regex
- Many false positives: casual conversation could accidentally trigger write operations

### After (New Behavior)
- Commands must use explicit `/command` syntax
- Freeform text defaults to chat mode (SideEffect.None)
- Only prefixed commands trigger workflow actions

## Migration for Users

### Old Usage (No Longer Works)
```
create a plan        → Treated as chat (no longer creates plan)
run the tests        → Treated as chat (no longer runs tests)  
fix the bug          → Treated as chat (no longer creates fix)
verify changes       → Treated as chat (no longer verifies)
```

### New Usage (Required)
```
/plan                → Creates/updates plan
/run                 → Executes current task
/fix                 → Creates fix plan
/verify              → Verifies execution
/status              → Shows status
/help                → Shows available commands
```

## Command Reference

### Workflow Commands (Write Operations)
| Command | Description |
|---------|-------------|
| `/run` | Execute the current task or workflow |
| `/plan` | Create or update a plan for the current phase |
| `/verify` | Verify the current task execution |
| `/fix` | Create a fix plan for identified issues |
| `/pause` | Pause the current workflow execution |
| `/resume` | Resume a paused workflow execution |

### Query Commands (Read-Only)
| Command | Description |
|---------|-------------|
| `/status` | Check the current workflow status |
| `/help` | Show available commands and usage |

### Chat Mode
- Any text without a `/` prefix is treated as freeform conversation
- Examples: "Hello", "What can you do?", "Explain this code"

## Confirmation Gate

Low-confidence classifications or ambiguous write operations may now require user confirmation:
- Default confirmation threshold: 0.9 confidence
- Affects non-explicit command patterns
- Can be configured via `ConfirmationGateOptions`

## API/Schema Changes

### New Types
- `CommandRegistry` - Registry of supported commands with side effect mappings
- `CommandParser` - Parses `/command` prefix syntax
- `IntentClassificationResult` - Enhanced classification result with metadata
- `ConfirmationGate` - Gate for ambiguous write operations
- `ParsedCommand` - Parsed command details

### Modified Types
- `InputClassifier.Classify()` now returns `IntentClassificationResult` instead of `Intent`
- Legacy method `ClassifyLegacy()` available for backward compatibility during transition

## Rollback Plan

If issues arise:
1. Revert the `InputClassifier.cs` changes to restore regex-based detection
2. Keep new components (`CommandRegistry`, `CommandParser`, etc.) for future use
3. Update documentation to reflect rollback

## Migration Checklist

- [ ] Update user documentation with new command syntax
- [ ] Train users on `/command` prefix requirement
- [ ] Update any scripts or automation using old keyword syntax
- [ ] Verify confirmation gate behavior with stakeholders
- [ ] Monitor for user confusion or support tickets

# Fix Planner Workflow Example

This document demonstrates the complete Fix Planner workflow from a failed UAT verification through to ready-to-execute fix tasks.

## Overview

The Fix Planner workflow closes the verification-fix loop in the execution pipeline by:
1. Consuming UAT verification failures (issues)
2. Analyzing root causes and affected files
3. Generating 2-3 concrete fix task plans
4. Routing to TaskExecutor for execution

## Example Workflow

### Step 1: UAT Verification Fails

When the UAT Verifier runs tests for task `TSK-001`, it discovers a bug:

**Input: Issue Artifact** (`.aos/spec/issues/ISS-0001.json`)
```json
{
  "schemaVersion": "gmsd:aos:schema:issue:v1",
  "id": "ISS-0001",
  "scope": "src/Calculator.cs",
  "repro": "Run unit test TestCalculator.Divide_ByZero_ReturnsInfinity",
  "expected": "Divide method should throw DivideByZeroException when divisor is 0",
  "actual": "Divide method returns Infinity instead of throwing exception",
  "severity": "high",
  "parentUatId": "criterion-001",
  "taskId": "TSK-001",
  "runId": "RUN-20260206-000001",
  "timestamp": "2026-02-06T06:30:00Z",
  "dedupHash": "a1b2c3d4e5f67890"
}
```

### Step 2: Gating Engine Routes to FixPlanner

The GatingEngine detects the verification failure and routes to FixPlanner:

```csharp
var context = new GatingContext
{
    LastVerificationStatus = "failed",
    IssueIds = new[] { "ISS-0001" },
    ParentTaskId = "TSK-001",
    // ... other context
};

// Result: TargetPhase = "FixPlanner"
// ContextData includes:
// - verificationStatus: "failed"
// - parentTaskId: "TSK-001"
// - issueIds: ["ISS-0001"]
```

### Step 3: FixPlanner Handler Processes Request

The `FixPlannerHandler` coordinates the fix planning:

```csharp
var request = new FixPlannerRequest
{
    IssueIds = new[] { "ISS-0001" },
    WorkspaceRoot = "/workspace",
    ParentTaskId = "TSK-001",
    ContextPackId = "ctx-verification-failure"
};

var result = await fixPlanner.PlanFixesAsync(request);
// result.FixTaskIds = ["TSK-FIX-TSK-001-001-a1b2c3d4"]
```

### Step 4: Fix Task Artifacts Generated

The FixPlanner generates three artifacts per fix task:

**Output: Task Metadata** (`TSK-FIX-TSK-001-001-a1b2c3d4/task.json`)
```json
{
  "schemaVersion": 1,
  "id": "TSK-FIX-TSK-001-001-a1b2c3d4",
  "type": "fix",
  "status": "planned",
  "parentTaskId": "TSK-001",
  "issueIds": ["ISS-0001"],
  "title": "Fix issue ISS-0001",
  "description": "Issues to fix:\n- ISS-0001 - Divide method should throw DivideByZeroException when divisor is 0 but found Divide method returns Infinity instead of throwing exception. Scope: src/Calculator.cs",
  "createdAt": "2026-02-06T06:31:00Z"
}
```

**Output: Fix Plan** (`TSK-FIX-TSK-001-001-a1b2c3d4/plan.json`)
```json
{
  "schemaVersion": 1,
  "taskId": "TSK-FIX-TSK-001-001-a1b2c3d4",
  "title": "Fix issue ISS-0001",
  "fileScopes": [
    {
      "relativePath": "src/Calculator.cs",
      "scopeType": "modify",
      "description": "Fix issues: ISS-0001"
    }
  ],
  "steps": [
    {
      "stepId": "step-001",
      "stepType": "modify_file",
      "targetPath": "src/Calculator.cs",
      "description": "Fix ISS-0001: Divide method should throw DivideByZeroException when divisor is 0"
    }
  ],
  "acceptanceCriteria": [
    {
      "id": "criterion-001",
      "description": "Divide method should throw DivideByZeroException when divisor is 0",
      "checkType": "verification",
      "isRequired": true
    },
    {
      "id": "criterion-002",
      "description": "All issues resolved and UAT passes",
      "checkType": "uat_pass",
      "isRequired": true
    }
  ]
}
```

**Output: Task Links** (`TSK-FIX-TSK-001-001-a1b2c3d4/links.json`)
```json
{
  "schemaVersion": 1,
  "parent": {
    "type": "task",
    "id": "TSK-001",
    "relationship": "fix-for"
  },
  "issues": [
    {
      "type": "issue",
      "id": "ISS-0001",
      "relationship": "fixes"
    }
  ]
}
```

### Step 5: State Updated

The state cursor is updated to indicate fix planning is complete:

**Before:**
```json
{
  "cursor": {
    "phaseId": "verifier",
    "phaseStatus": "completed",
    "taskId": "TSK-001",
    "taskStatus": "failed",
    "stepStatus": "failed"
  }
}
```

**After:**
```json
{
  "cursor": {
    "milestoneId": "milestone-1",
    "milestoneStatus": "active",
    "phaseId": "fix-planner",
    "phaseStatus": "completed",
    "taskId": "TSK-001",
    "taskStatus": "fix-planned",
    "stepId": "TSK-FIX-TSK-001-001-a1b2c3d4",
    "stepStatus": "ready-to-execute"
  }
}
```

### Step 6: Events Recorded

Lifecycle events are appended to `events.ndjson`:

```json
{"eventType":"fix-planning.started","parentTaskId":"TSK-001","issueIds":["ISS-0001"],"correlationId":"ctx-verification-failure","timestamp":"2026-02-06T06:31:00Z"}
{"eventType":"fix-planning.completed","parentTaskId":"TSK-001","fixTaskIds":["TSK-FIX-TSK-001-001-a1b2c3d4"],"issueCount":1,"taskCount":1,"correlationId":"ctx-verification-failure","timestamp":"2026-02-06T06:31:00Z"}
```

### Step 7: Handler Routes to TaskExecutor

The `FixPlannerHandler` returns success with routing hint:

```csharp
return new CommandRouteResult
{
    IsSuccess = true,
    Output = "Fix planning completed for TSK-001. Created 1 fix task(s) for 1 issue(s). First fix task: TSK-FIX-TSK-001-001-a1b2c3d4",
    RoutingHint = "TaskExecutor"
};
```

### Step 8: Fix Tasks Ready for Execution

The fix task is now ready for the TaskExecutor to execute:

1. Task artifacts are in `.aos/spec/tasks/TSK-FIX-TSK-001-001-a1b2c3d4/`
2. State cursor indicates `ready-to-execute` status
3. GatingEngine will route to Executor on next cycle
4. TaskExecutor will apply the fix to `src/Calculator.cs`

## Summary

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  UAT Verifier   │────▶│   FixPlanner    │────▶│  TaskExecutor   │
│   (fails)       │     │  (generates)    │     │  (executes)     │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │                       │
        ▼                       ▼                       ▼
   ISS-0001.json           TSK-FIX-*.json         Fixed code
   (issue artifact)        (fix task plan)        (applied fix)
```

## Files Generated

| File | Location | Purpose |
|------|----------|---------|
| Input Issue | `.aos/spec/issues/ISS-*.json` | UAT verification failure record |
| Task Metadata | `.aos/spec/tasks/TSK-FIX-*/task.json` | Fix task identity and status |
| Fix Plan | `.aos/spec/tasks/TSK-FIX-*/plan.json` | Steps, scopes, acceptance criteria |
| Task Links | `.aos/spec/tasks/TSK-FIX-*/links.json` | Relationships to parent task and issues |
| State | `.aos/state/state.json` | Updated cursor position |
| Events | `.aos/state/events.ndjson` | Lifecycle audit trail |

# Design: Write-Side-Effect Confirmation Gate

## Architecture Overview

The confirmation gate acts as a **control plane filter** that intercepts workflow transitions requiring user approval. It sits between the **intent classification** and **phase dispatch** stages of the orchestrator.

```
User Input → Intent Classification → Confirmation Gate → Phase Dispatch
                                    ↓
                              (if confirmation needed)
                                    ↓
                         Emit confirmation.requested
                                    ↓
                         Wait for confirmation.response
                                    ↓
                         Resume or Abort
```

## Key Design Decisions

### 1. Gate Position in Orchestrator Flow

The confirmation gate operates at **three distinct points**:

1. **Pre-classification**: Validate workspace prerequisites (spec, state exist)
2. **Post-classification**: Evaluate confidence/destructiveness for write operations
3. **Pre-execution**: Final safety check before file/git mutations

### 2. Confirmation State Machine

```
[Evaluating] → [ConfirmationRequired] → [PendingUserResponse]
      ↓               ↓                        ↓
   [Allow]      [Timeout]               [Accepted]
                                    ↓
                                 [Rejected]
```

States are persisted in `.aos/state/confirmations.json`:
```json
{
  "pending": [
    {
      "id": "abc123",
      "state": "PendingUserResponse",
      "requestedAt": "2026-02-11T19:30:00Z",
      "timeout": "00:05:00",
      "action": { "phase": "Executor", "affectedFiles": [...] }
    }
  ]
}
```

### 3. Event Protocol

Confirmation events extend the streaming dialogue protocol:

```csharp
// confirmation.requested
public record ConfirmationRequestedEvent : IStreamingEvent
{
    public string ConfirmationId { get; init; }
    public ProposedAction Action { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public string Reason { get; init; }
    public TimeSpan? Timeout { get; init; }
}

// confirmation.responded
public record ConfirmationRespondedEvent : IStreamingEvent
{
    public string ConfirmationId { get; init; }
    public bool Accepted { get; init; }
    public string? UserMessage { get; init; }
}
```

### 4. Risk Level Hierarchy

Extends existing `RiskLevel` enum:

```csharp
public enum RiskLevel
{
    Read,                    // No side effects
    WriteSafe,              // Creates new files, idempotent writes
    WriteDestructive,       // Modifies existing files
    WriteDestructiveGit,    // Git commits, pushes (irreversible)
    WorkspaceDestructive    // Deletes/corrupts workspace state
}
```

### 5. Prerequisite Validation Strategy

Instead of failing when prerequisites are missing, the gate **converts the failure into a conversational action**:

```csharp
public class PrerequisiteCheckResult
{
    public bool IsSatisfied { get; init; }
    public MissingPrerequisite? Missing { get; init; }
    public ProposedAction? RecoveryAction { get; init; }  // "Ask user to initialize"
}
```

Example recovery flow:
1. User requests: `"plan the foundation phase"`
2. Gate detects: No project spec exists
3. Instead of throwing, emit: `assistant.final` with "I need to create a project first. Should I start the interviewer?"
4. User confirms: Execute interviewer workflow

### 6. Structured ProposedAction

The gate forces the model (or deterministic logic) to output a validated `ProposedAction` before execution:

```csharp
public class ProposedAction
{
    public string Phase { get; init; }
    public string Description { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public IReadOnlyList<string> AffectedResources { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    
    // Validation method
    public ValidationResult Validate()
    {
        // Ensure description is meaningful
        // Ensure affected resources are within scope
        // Verify risk level matches phase
    }
}
```

### 7. Integration with Existing Components

**ConfirmationGate** (exists) → Add methods:
- `EvaluatePrerequisites(GatingContext)`
- `EvaluateDestructiveness(ProposedAction)`

**GatingEngine** (exists) → Wire confirmation check:
- Before returning `GatingResult`, pass through `IConfirmationGate`
- If confirmation required, return `GatingResult` with `RequiresConfirmation=true`

**Orchestrator** (exists) → Handle confirmation pause:
- Check `GatingResult.RequiresConfirmation`
- If true: emit event, pause run, await response
- On resume: continue to phase dispatch

## Data Flow

### Scenario 1: Ambiguous Write Operation

```
User: "execute the plan"
  ↓
IntentClassification: { Kind: WorkflowCommand, SideEffect: Write, Confidence: 0.65 }
  ↓
ConfirmationGate.Evaluate():
  - Confidence 0.65 < threshold 0.8
  - RiskLevel: WriteDestructive
  → RequiresConfirmation
  ↓
Emit: confirmation.requested {
  id: "conf-001",
  action: { phase: "Executor", description: "Execute tasks in current plan" },
  riskLevel: WriteDestructive,
  reason: "Low confidence (0.65) and destructive side effects"
}
  ↓
UI renders confirmation dialog
  ↓
User clicks "Confirm"
  ↓
Emit: confirmation.responded { id: "conf-001", accepted: true }
  ↓
Orchestrator resumes → Phase dispatch
```

### Scenario 2: Missing Prerequisites

```
User: "plan phase PH-0001"
  ↓
PrerequisiteCheck:
  - HasProject: false
  - HasRoadmap: false
  → MissingPrerequisite: ProjectSpec
  ↓
Emit: assistant.final {
  message: "I need a project specification before planning phases. 
           Would you like me to start the project interviewer?",
  suggestedCommand: "/interview"
}
  ↓
User: "yes"
  ↓
Proceed to Interviewer phase
```

## Error Handling

### Timeout Handling

```csharp
// Background task monitors pending confirmations
if (confirmation.Expired)
{
    Emit confirmation.timeout
    Cancel associated run
    Cleanup pending state
}
```

### Duplicate Confirmation Prevention

```csharp
// Hash of (action description + affected resources)
var confirmationKey = ComputeHash(action);
if (_pendingConfirmations.ContainsKey(confirmationKey))
{
    return ExistingConfirmationResult(confirmationKey);
}
```

## Testing Strategy

1. **Unit tests**: Mock `IDestructivenessAnalyzer`, test gate logic
2. **Integration tests**: Full orchestrator flow with fake LLM
3. **Snapshot tests**: Confirm event serialization stability

## Future Extensions

- **Granular permissions**: Per-user confirmation requirements
- **Batch confirmations**: Group multiple low-risk actions
- **Learned preferences**: Auto-confirm based on user history

## ADDED Requirements
### Requirement: Orchestrator performs deterministic workspace-state startup hook before dispatch
Before workflow phase dispatch, the orchestrator SHALL invoke `EnsureWorkspaceInitialized()` to validate and repair baseline workspace state artifacts deterministically.

`EnsureWorkspaceInitialized()` MUST run before any write-phase dispatch and before run evidence finalization that depends on state snapshot availability.

#### Scenario: Initialization succeeds and write-phase dispatch continues
- **GIVEN** a write-capable request that would route to Planner or Executor
- **WHEN** orchestrator execution begins
- **THEN** it invokes `EnsureWorkspaceInitialized()` before phase handler dispatch
- **AND** phase dispatch proceeds only after readiness succeeds

#### Scenario: Healthy workspace incurs no-op startup behavior
- **GIVEN** a workspace with valid baseline state artifacts already present
- **WHEN** orchestrator invokes startup readiness
- **THEN** readiness completes without mutating baseline state files
- **AND** orchestrator continues through normal gating and dispatch

### Requirement: Orchestrator converts unrecoverable state readiness failures into conversational output
If startup readiness cannot establish deterministic state prerequisites, the orchestrator SHALL stop workflow execution and emit a conversational recovery response with a suggested fix action.

The conversational response MUST include:
- a clear explanation of the failed preflight readiness condition
- a suggested corrective command or action
- an explicit statement that no workflow phase dispatch was performed

#### Scenario: Initialization fails and write-phase dispatch is blocked
- **GIVEN** startup readiness fails because state derivation cannot complete
- **WHEN** orchestrator handles the preflight result
- **THEN** it emits assistant-facing conversational output describing the failure and suggested remediation
- **AND** it does not dispatch any write-phase workflow handler
- **AND** it records the preflight failure outcome in run diagnostics when a run context exists

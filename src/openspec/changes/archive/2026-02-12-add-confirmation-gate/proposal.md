# Change: Add Confirmation Gate for Write-Side-Effect Operations

## Why

The orchestrator currently creates runs automatically for write-classified intents without user confirmation, leading to accidental side effects. The Remediation.md (lines 98-111) identifies that users need a safety gate before any operation that modifies files, git state, or workspace artifacts. Without this gate, ambiguous requests get executed, destructive operations proceed unchecked, and missing prerequisites cause failures rather than conversational recovery.

## What Changes

- **ADDED**: Confirmation gate evaluation before dispatching to workflow phases with `SideEffect.Write`
- **ADDED**: Ambiguous request detection that triggers confirmation even when classified as write
- **ADDED**: Destructive operation detection integration (file modifications, git commits) requiring mandatory confirmation
- **ADDED**: Prerequisite validation that converts missing workspace requirements into conversational assistant responses instead of hard failures
- **ADDED**: Structured `ProposedAction` output validation using LLM structured outputs
- **ADDED**: Streaming events for `confirmation.requested`, `confirmation.accepted`, `confirmation.rejected`, `confirmation.timeout`
- **ADDED**: State persistence for pending confirmations in `.aos/state/confirmations.json`
- **MODIFIED**: Gating engine to integrate prerequisite validation, confidence threshold, and destructiveness analysis before phase routing

## Impact

- **Affected specs**: `confirmation-gate`, `destructive-operation-detection`, `prerequisite-validation`, `intent-classification`, `orchestrator-event-emitter`
- **Affected code**: 
  - `nirmata.Agents` gating engine and orchestrator workflow
  - `nirmata.Aos` state store for confirmation persistence
  - Event streaming protocol for confirmation lifecycle events
- **Breaking changes**: None - this adds new safety behavior; existing write operations will now pause for confirmation but can proceed when confirmed
- **User experience**: Users will see explicit confirmation prompts before any write operation with details about what will happen, risk level, and affected resources

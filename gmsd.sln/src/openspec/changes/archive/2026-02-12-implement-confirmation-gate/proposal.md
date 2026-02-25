# Implement Write-Side-Effect Confirmation Gate

**Change ID:** `implement-confirmation-gate`

## Why

The orchestrator currently lacks a comprehensive confirmation gate system before executing write-side-effect operations. According to the Remediation Report (lines 98-107), the system needs:

1. Confirmation for ambiguous requests (low-confidence classifications)
2. Confirmation for destructive operations (file modifications, git commits)
3. Conversational fallback when workspace prerequisites are missing (ask rather than fail)

Without this gate, the system may execute destructive operations without user approval, leading to unintended file changes, git commits, or workspace corruption.

## What Changes

### confirmation-gate (NEW spec)
- Define confirmation gate behavior for intercepting write-side-effect operations
- Specify confidence threshold evaluation rules
- Define confirmation request/response event protocol
- Specify confirmation state persistence requirements
- Define timeout and duplicate prevention mechanisms

### destructive-operation-detection (NEW spec)
- Define destructiveness analysis for file system operations
- Specify git operation risk classification (WriteDestructiveGit)
- Define scope-aware destructiveness analysis
- Specify configuration-based destructiveness overrides

### prerequisite-validation (NEW spec)
- Define workspace prerequisite validation before workflow execution
- Specify conversational recovery instead of hard failures
- Define prerequisite-aware gating context
- Specify workspace bootstrap detection

## Problem Statement

The orchestrator currently lacks a comprehensive confirmation gate system before executing write-side-effect operations. According to the Remediation Report (lines 98-107), the system needs:

1. Confirmation for ambiguous requests (low-confidence classifications)
2. Confirmation for destructive operations (file modifications, git commits)
3. Conversational fallback when workspace prerequisites are missing (ask rather than fail)

While some infrastructure exists (`ConfirmationGate`, `GatingEngine`, `DestructivenessAnalyzer`), the complete user-facing confirmation flow with streaming events and proper integration into the orchestrator lifecycle is not yet implemented.

## Goals

1. Implement a robust confirmation gate that intercepts write operations before they execute
2. Require explicit user confirmation for destructive or ambiguous operations
3. Emit structured streaming events for confirmation requests/responses
4. Enable conversational recovery when prerequisites are missing
5. Ensure the gate integrates cleanly with the orchestrator workflow

## Scope

**In Scope:**
- Confirmation gate evaluation logic (confidence thresholds, destructiveness analysis)
- Streaming event protocol for confirmation UI integration
- Prerequisite validation with conversational fallback
- Integration with existing `IConfirmationGate`, `IGatingEngine`, and `IDestructivenessAnalyzer`

**Out of Scope:**
- LLM provider implementation (assumes existing infrastructure)
- Full chat responder implementation (covered by separate change)
- UI rendering of confirmation dialogs (UI layer responsibility)

## Proposed Solution

The solution builds upon existing infrastructure:

1. **Extend `ConfirmationGate`** to support prerequisite validation and missing-workspace-state detection
2. **Define streaming events** for `confirmation.requested`, `confirmation.accepted`, `confirmation.rejected`, `confirmation.timeout`
3. **Integrate with orchestrator** to pause/resume execution based on confirmation state
4. **Add `ProposedAction` structured output** for validation before execution

## References

- Remediation Report: `openspec/Remediation.md` lines 98-107
- Existing spec: `openspec/specs/intent-classification/spec.md` (requirements around confirmation)
- Existing implementation: `Gmsd.Agents/Execution/Preflight/ConfirmationGate.cs`
- Gating engine: `Gmsd.Agents/Execution/ControlPlane/GatingEngine.cs`

## Success Criteria

- [x] All write-side-effect operations trigger confirmation evaluation
- [x] Low-confidence classifications require explicit user confirmation
- [x] Destructive operations (file writes, git commits) require explicit confirmation
- [x] Missing prerequisites trigger conversational ask rather than hard failure
- [x] Streaming events enable UI to render confirmation dialogs
- [x] Confirmation state is persisted for run resumability


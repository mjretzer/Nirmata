# Proposal: Implement Subagent Execution Loop

## Problem

The current `TaskExecutor` has a skeleton for tool calling but lacks the robust subagent execution loop required for "Real Execution" as defined in Phase 6 of the remediation plan. Specifically, it needs a tighter integration with the tool calling loop, a dedicated "scope firewall" to prevent out-of-scope modifications, and comprehensive evidence capture including diffs and tool logs.

## Proposed Changes

1.  **Robust Subagent Execution Loop**: Refine the `TaskExecutor` and `ToolCallingLoop` integration to ensure it follows the "plan-step-execute" pattern effectively.
2.  **Scope Firewall**: Implement a strict validation layer that prevents tools from reading or writing outside the allowed task scope. This should be enforced at the tool registry or execution level, not just as a check before calling the loop.
3.  **Verification Tools**: Implement tools for running build and test commands (`dotnet build`, `dotnet test`) and parsing their results into UAT (User Acceptance Testing) artifacts.
4.  **Comprehensive Evidence**: Enhance evidence capture to include:
    *   Full tool calling logs (request/response).
    *   Deterministic diffs for all modified files.
    *   A final execution summary.
    *   A deterministic hash of the entire execution result.

## Goals

*   Reliable execution of task plans using a subagent loop.
*   Guaranteed safety through strict file scoping (scope firewall).
*   Automatic verification of changes through build/test tools.
*   Audit-ready evidence artifacts for all executions.

# Proposal: Refine Orchestrator Flow

## What Changes
This proposal aims to align the existing orchestrator implementation with the canonical flow described in the project documentation. The current implementation is functional but uses component names and a workflow that are not fully aligned with the documented roles (e.g., `DirectAgentRunner` vs. `Subagent Orchestrator`).

## Why
Aligning the implementation with the documentation will improve maintainability, reduce cognitive overhead for new developers, and ensure the system behaves as designed. This change will refactor the control loop to explicitly follow the `classify -> load -> validate -> gate -> dispatch -> validate -> persist -> return` pattern and adopt the canonical component names.

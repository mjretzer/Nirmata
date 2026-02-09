# Design: Orchestrator Flow Refinement

## 1. Overview

The primary goal is to refactor the existing implementation to match the canonical orchestrator flow. This involves introducing explicit validation steps, clarifying the dispatch mechanism (especially for subagents), and renaming components to match their documented roles.

## 2. Component Mapping

The following mapping will guide the refactoring:

- **`WorkflowClassifier`** will be refactored or replaced to better represent its role as the initial classifier and entry point.
- **`GatingEngine`** will be confirmed to align with the state-machine transition rules.
- **`RunLifecycleManager`** will be enhanced to handle the full `aos run start/finish` cycle, evidence attachment, and state persistence.
- Handlers like **`RoadmapperHandler`** will be treated as specialist workflows selected by the gating process.
- A **`Subagent Orchestrator`** concept will be introduced to handle isolated execution of plan steps.

## 3. Control Loop Enhancement

The `Orchestrator.ExecuteAsync` method will be updated to explicitly include:

1.  **Pre-flight Validation**: A new service will be introduced to validate schemas and relevant artifact slices before gating.
2.  **Context Pack Generation**: A call to a `ContextEngineer` or similar service will be added to build a context pack before dispatch.
3.  **Post-execution Validation**: After a specialist handler returns, its outputs and contracts will be validated before persistence.

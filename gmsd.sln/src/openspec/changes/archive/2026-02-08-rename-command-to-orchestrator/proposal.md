# rename-command-to-orchestrator — Proposal

## Why
The current "Command Center" page (`/Command`) has a generic name that doesn't reflect its actual purpose in the GMSD architecture. The page serves as the primary interface for users to interact with the agent orchestration system and command subagents to perform work. Renaming it to "Orchestrator" aligns the UI terminology with the underlying `IOrchestrator` and subagent orchestration concepts defined in `agents-subagent-orchestration` and `orchestrator-workflow` specs.

This change clarifies the user's mental model: they are not just "issuing commands" but "orchestrating agents" through a unified interface that supports both CLI-style commands and freeform natural language inputs.

## What Changes
- **Rename** `Pages/Command/` to `Pages/Orchestrator/`
- **Rename** route from `/Command` to `/Orchestrator`
- **Update** navigation links and references throughout the codebase
- **Refactor** page model to support subagent command flow:
  - CLI command parsing (`/status`, `/validate`, `/run`, etc.)
  - Freeform text handling (natural language intent)
- **Integrate** with `WorkflowClassifier` for in-process orchestration
- **Preserve** all existing functionality (workspace status, safety rails, evidence display)

## Impact
- **Affected specs**: `web-orchestrator-page` (new capability delta), `web-agent-runner` (references)
- **Affected code**: 
  - `Gmsd.Web/Pages/Command/` → `Gmsd.Web/Pages/Orchestrator/`
  - `Gmsd.Web/AgentRunner/WorkflowClassifier.cs` (namespace references if any)
  - Navigation components referencing the old route
- **Breaking change**: URL `/Command` will redirect to `/Orchestrator`

# Change: Add Backlog Capture & Triage Plane

## Why
The engine needs a dedicated plane to capture work items (TODOs, issues) without interrupting the current execution flow. This allows the system to safely defer non-urgent items while ensuring urgent issues get routed into the main execution loop. Currently, there is no structured mechanism for triage or deferred item management.

## What Changes
- **ADDED** New capability `backlog-triage` with three sub-components:
  - `DeferredIssuesCurator` — triages issues from `.aos/spec/issues/` and routes urgent items
  - `TodoCapturer` — captures TODOs to `.aos/context/todos/` without affecting cursor
  - `TodoReviewer` — reviews captured TODOs and promotes them to tasks or roadmap phases
- **ADDED** Workspace file contracts for TODOs (`TODO-*.json`) and issues (`ISS-*.json`)
- **ADDED** Event logging for triage and capture operations in `.aos/state/events.ndjson`
- **Impact on Gmsd.Agents**: New `Execution/Backlog/` directory structure with handlers and workflows

## Impact
- **Affected specs:** NEW capability `backlog-triage`
- **Affected code:** 
  - `Gmsd.Agents/Execution/Backlog/DeferredIssuesCurator/**`
  - `Gmsd.Agents/Execution/Backlog/TodoCapturer/**`
  - `Gmsd.Agents/Execution/Backlog/TodoReviewer/**`
- **Workspace outputs:** `.aos/context/todos/`, `.aos/spec/issues/`, `.aos/state/events.ndjson`

## Success Criteria
1. Urgent issue yields a deterministic routing recommendation (into main loop)
2. TODO capture does not change cursor unless explicitly promoted
3. Deferred queue is separate from roadmap; triage routes urgent items correctly

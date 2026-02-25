# Change: Add Advanced UI Pages (Fix Planning, Intelligence, State, Checkpoints, Validation)

## Why
The GMSD Web UI currently provides basic project/run management, but lacks visibility into the engine's internal state and advanced orchestration features. Operators need UI access to fix planning workflows, codebase intelligence artifacts, context packs, state/events history, checkpoint management, and validation tools to effectively monitor and control agent runs.

## What Changes
- **ADDED** Fix Planning Page (`/Fix`) — view repair loops, plan/execute fixes, re-verify outcomes
- **ADDED** Codebase Intelligence Page (`/Codebase`) — trigger scans, view intelligence artifacts (map, stack, architecture, symbols, graph)
- **ADDED** Context Packs Page (`/Context`) — list packs by task/phase, build/show/diff packs
- **ADDED** State & Events Page (`/State`) — view state.json, tail events.ndjson with filtering, history summary
- **ADDED** Pause/Resume & Checkpoints Page (`/Checkpoints`) — create/restore checkpoints, view handoff.json, manage locks
- **ADDED** Validation & Maintenance Page (`/Validation`) — run validations, repair indexes, clear/prune cache
- **ADDED** Navigation links in shared layout for all new pages

## Impact
- **Affected specs:** `web-razor-pages`, `web-runs-dashboard` (navigation updates)
- **Affected code:** `Gmsd.Web/Pages/**` (new pages), `Gmsd.Web/Pages/Shared/_Layout.cshtml` (nav updates)
- **Dependencies:** Requires `ICheckpointManager`, `IEventStore`, `IStateStore`, codebase mapping artifacts from `Gmsd.Aos`
- **Non-breaking:** All changes are additive UI features; no API or schema changes

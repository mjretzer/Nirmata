## Why

The control-plane gate sequence now exists as a deterministic artifact-driven workflow, but the UI still makes users infer the current gate from scattered page state and partial page-local cues. The first UI follow-up should surface that workflow directly so users can see the current blocking step, understand why they are blocked, and navigate to the correct action without inspecting raw `.aos` artifacts.

## What Changes

- Add a workspace-level gate status surface that shows the current orchestrator gate, the next required action, and the artifact-backed reason that gate is active.
- Surface brownfield preflight state and codebase-map readiness as explicit status states instead of leaving them implicit in Codebase or roadmap failures.
- Add clear route-to-action affordances from the status surface into the page or workflow that can resolve the active gate.
- Define the workspace data contract needed for the UI to consume a canonical gate summary and readiness details from backend-derived workspace state.

## Capabilities

### New Capabilities
- `gate-status-surface`: Define the UI requirements for showing the current workspace gate, blocking reason, readiness details, and next action across dashboard, chat, and codebase flows.

### Modified Capabilities
- `workspace-domain-data`: Add a workspace-scoped gate/status summary contract so clients can read the current gate, next action, and brownfield readiness details from canonical artifacts.

## Impact

- Affected frontend areas: workspace dashboard, chat, and codebase surfaces in `nirmata.frontend` plus shared workspace status hooks/components.
- Affected backend areas: workspace-scoped read models or endpoints in `nirmata.Api` and supporting services that derive gate status from canonical `.aos/spec`, `.aos/state`, and `.aos/codebase` artifacts.
- Affected UX: users get a deterministic top-level explanation of why work is blocked and where to go next before deeper page-specific UX phases land.
- No intended change to the orchestrator gate order itself; this proposal is about exposing the already-aligned control-plane state in a user-visible way.
## Why

`ChatPage` already has a durable, workspace-scoped thread from Phase 1. Phase 2 turns the `History` affordance into a real drawer so operators can inspect the current workspace thread without leaving the page or switching to a separate session model.

## What Changes

- Add a dedicated `History` drawer for the active workspace thread.
- Surface per-turn metadata such as timestamp, role, gate state, run id, and next command.
- Keep the current chat composer and main thread as the primary interaction surface.
- Add a quick path back to the active composer from the drawer.
- Reuse the existing live snapshot contract unless a small thread-summary field is needed to keep repeated drawer opens cheap.

## Impact

- `nirmata.frontend` gains a usable workspace-thread history experience instead of a placeholder action.
- `nirmata.Api` may expose lightweight thread metadata if the drawer needs summary information beyond the existing snapshot payload.
- Operators can review prior turns in context while keeping the current workspace thread as the source of truth.
- Verification expands to cover drawer behavior, metadata rendering, and state preservation.

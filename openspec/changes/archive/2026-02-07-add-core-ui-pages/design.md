# Design: Core AOS UI Pages

## Context
The AOS engine uses `.aos/*` workspace for spec-first development. Users need a UI surface to interact with this workspace: select it, view state, issue commands, browse specs, and edit configuration. This spans 5 distinct functional areas that together make the engine operational.

## Goals / Non-Goals

**Goals:**
- Provide workspace selection and validation UI
- Surface current engine state (cursor position, blockers, next actions)
- Enable command input via chat-style interface with slash commands
- Allow browsing and editing of AOS artifacts (specs, roadmaps, phases, tasks)
- Enable project spec editing via guided interview mode

**Non-Goals:**
- Real-time collaboration (multi-user)
- Mobile-responsive design (desktop-first)
- Custom theming beyond dark/light mode
- Integration with external project management tools

## Decisions

### Decision: Page-per-Concern Routing
Use dedicated Razor Pages for each functional area rather than a SPA:
- **Rationale:** Simpler implementation, leverages existing `web-razor-pages` infrastructure, progressive enhancement possible
- **Trade-off:** Full page reloads on navigation; acceptable for operational tooling

### Decision: Server-Side Rendering with HTMX for Interactivity
Use HTMX for partial updates (chat streaming, form validation) without full SPA complexity:
- **Rationale:** Keeps logic in C#, minimal JavaScript, fits existing Razor Pages stack
- **Trade-off:** Less fluid than React/Vue; acceptable for this use case

### Decision: Direct File System Access for Workspace
Web pages read directly from `.aos/*` via AOS contracts (not via API layer):
- **Rationale:** `Gmsd.Web` is the operational UI; direct access matches `DirectAgentRunner` pattern
- **Trade-off:** Tighter coupling to AOS file contracts; acceptable for this project phase

### Decision: Read-Only First, Edit-Second
Phase 1 focuses on viewing/exploring; editing features are Phase 2 enhancements:
- **Rationale:** Minimizes initial complexity, validates UX before adding mutation paths

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Workspace path traversal attacks | Validate paths are within allowed roots; use Path.GetFullPath + prefix check |
| Large spec files cause slow renders | Implement pagination for large lists; lazy-load file contents |
| Concurrent edits corrupt JSON | File locking or atomic writes via temp+rename pattern |
| Chat UI feels unresponsive | HTMX loading indicators; consider Server-Sent Events for streaming |

## Migration Plan
These are new pages; no migration needed. Navigation links will be added to existing `_Layout.cshtml`.

## Open Questions
- Should workspace selection persist per-user or per-machine? (Starting with per-machine via config)
- Should chat history persist across sessions? (Defer to Phase 2)

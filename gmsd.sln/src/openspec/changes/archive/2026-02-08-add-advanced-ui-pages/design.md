# Design: Advanced UI Pages

## Context
These pages provide UI visibility into AOS engine internals that are currently only accessible via file inspection or API calls. The pages bridge the product Web UI with engine workspace artifacts under `.aos/*`.

## Goals
- Provide operators with visual access to engine state, events, and intelligence
- Enable runtime debugging and troubleshooting without file system access
- Support checkpoint/restore workflows for run continuity
- Surface validation and maintenance tools

## Non-Goals
- Full workflow orchestration (remains in Gmsd.Agents plane)
- Write access to engine state (read-only visualization only, except explicit actions)
- Replacing command-line tools for advanced operations

## Decisions

### Decision: Page-per-concern organization
**What:** Each major concern (Fix, Codebase, Context, State, Checkpoints, Validation) gets its own top-level page group under `Pages/{Concern}/`.  
**Why:** Matches the AOS workspace structure (`.aos/fix/`, `.aos/codebase/`, `.aos/context/`, etc.) and allows each page to grow independently.  
**Alternative considered:** Single "Engine" page with tabs — rejected due to complexity and different refresh patterns per concern.

### Decision: Read-only with explicit actions
**What:** Pages display workspace artifacts read-only; mutations happen only via explicit button actions that delegate to engine services.  
**Why:** Maintains separation of concerns — Web UI visualizes, AOS services mutate.  
**Safety:** All actions validate workspace state before executing.

### Decision: JSON viewer component reuse
**What:** Create a reusable `JsonViewer` component for displaying structured JSON artifacts (state.json, events, codebase intelligence).  
**Why:** Multiple pages need to display formatted JSON with collapsible sections.  
**Pattern:** Shared partial view `Shared/_JsonViewer.cshtml` with model `JsonViewerModel`.

### Decision: Server-side rendering with HTMX for partial updates
**What:** Use Razor Pages with HTMX for dynamic sections (event tail, run status).  
**Why:** Aligns with existing Gmsd.Web patterns (no SPA framework needed).  
**Fallback:** Full page refresh works without JavaScript.

### Decision: Delegation to engine services
**What:** Web pages depend only on public AOS interfaces (`ICheckpointManager`, `IEventStore`, `IStateStore`, etc.), not internal engine types.  
**Why:** Enforces the product/engine boundary per `project.md` separation of concerns.  
**Location:** Service resolution via DI in page constructors.

## Risks / Trade-offs
- **Risk:** Page load performance with large event logs → Mitigation: Pagination/tailing with configurable limits  
- **Risk:** Concurrent workspace modifications during visualization → Mitigation: Read-only display, lock-aware actions  
- **Trade-off:** Real-time updates vs simplicity → HTMX polling (simple) over WebSockets (complex)

## Navigation Structure
```
GMSD (header)
├── Projects
├── Runs
├── Issues
├── Codebase (NEW) → Scan | Map | Stack | Architecture | Symbols | Graph
├── Context (NEW) → Packs list | Build | Show | Diff
├── State (NEW) → state.json | Events | History
├── Checkpoints (NEW) → List | Create | Restore | Locks
├── Validation (NEW) → Validate | Repair | Cache
└── Fix (NEW) → Repair Loop | Plan | Execute | Re-verify
```

## Open Questions
- Should event tailing auto-refresh? If so, what interval?
- Should checkpoint restore require confirmation modal?
- Should validation failures link directly to offending files?

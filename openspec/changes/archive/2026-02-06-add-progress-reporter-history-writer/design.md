# Design: Progress Reporter and History Writer

## Context

The Continuity Plane is responsible for pause/resume, progress visibility, and narrative history. The existing `PauseResumeManager` handles interruption-safe snapshots. This change adds two complementary capabilities:

1. **Progress Reporter**: Real-time visibility into execution state (cursor, blockers, next actions)
2. **History Writer**: Durable, verifiable narrative of completed work with evidence links

Both capabilities read from existing AOS stores (state, evidence) and produce human-readable outputs in the workspace.

## Goals / Non-Goals

**Goals:**
- Deterministic progress reports that match state/roadmap exactly
- Durable history entries with verifiable evidence pointers
- Safe concurrent access to summary.md append operations
- Integration with existing stores (no new persistence layer)

**Non-Goals:**
- Real-time streaming updates (polling-based is acceptable)
- External notification systems (webhooks, etc.)
- History modification or deletion (append-only)
- Rich formatting or rendering (markdown tables are sufficient)

## Decisions

### Decision: History entries appended to `.aos/spec/summary.md`
- **Rationale**: `spec/` contains intended truth; history is the narrative of what was intended and completed
- **Alternative considered**: `.aos/evidence/summary.md` — rejected because evidence/ is for provable truth (artifacts), not narrative summaries

### Decision: Evidence pointers link to `RUN-*/summary.json`
- **Rationale**: Summary.json is the stable entry point for run evidence; other artifacts are linked from there
- **Format**: Relative path from workspace root: `.aos/evidence/runs/RUN-0001/summary.json`

### Decision: Progress reports are stateless reads
- **Rationale**: Progress is derived from current state on each request; no caching or snapshots needed
- **Implication**: Report reflects state at moment of request, not fixed point in time

### Decision: Append-only history with file locking
- **Rationale**: Concurrent runs may attempt to write history simultaneously
- **Implementation**: Use filesystem-level append with OS-level atomicity, or implement simple file lock in cache/

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Concurrent history writes corrupt summary.md | Use atomic file append or implement lock file in `.aos/cache/` |
| Evidence pointers become stale (run deleted) | Store full metadata in history entry; stale pointer is detectable |
| Progress report outdated due to stale state | Document that reports are point-in-time; refresh for latest |
| Commit hash unavailable (non-git workspace) | Make commit hash optional; record as null when unavailable |

## Migration Plan

No migration needed — this is new capability. Existing workspaces without summary.md will have it created on first history write.

## Open Questions

- [ ] Should history entries include LLM token usage statistics?
- [ ] Should progress reports include estimated time remaining?

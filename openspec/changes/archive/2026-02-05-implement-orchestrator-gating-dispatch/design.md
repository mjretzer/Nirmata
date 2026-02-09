## Context

The orchestrator is the control-plane heart of `Gmsd.Agents`. It implements the "classify → gate → dispatch → validate → persist → next" workflow loop that determines what phase of work an agent should perform based on workspace state.

Current state:
- `Gmsd.Aos` provides public APIs: `ICommandRouter`, `IWorkspace`, `ISpecStore`, `IStateStore`, `IValidator`
- `Gmsd.Agents` has basic `IRunRepository` and `RunRepository` (in-memory stub)
- No unified orchestration layer exists yet

## Goals / Non-Goals

**Goals:**
- Implement gating logic that routes to correct phase based on workspace state
- Provide clean DI-based service injection (no process spawning)
- Create run lifecycle with evidence capture
- Support the 6 gating outcomes: Interviewer, Roadmapper, Planner, Executor, Verifier, FixPlanner

**Non-Goals:**
- Implementing the actual phase handlers (Interviewer, Roadmapper, etc.) - only the routing
- CLI interface for the orchestrator (it's a library API)
- LLM-specific logic (stays in adapters)

## Decisions

### Decision: Direct service injection over CLI invocation
- **Rationale**: Performance, type safety, testability. The orchestrator runs in-process with the host.
- **Alternatives considered**: CLI subprocess spawning - rejected due to overhead and complexity

### Decision: Gating engine as separate service
- **Rationale**: Separation of concerns - gating rules can be tested independently
- **Approach**: `IGatingEngine` evaluates `GatingContext` → returns `GatingResult` with target phase

### Decision: Run lifecycle wraps Engine stores via repository pattern
- **Rationale**: `Gmsd.Agents` shouldn't directly access Engine internals; `IRunRepository` provides the boundary
- **Implementation**: `RunLifecycleManager` implements `IRunLifecycleManager` using injected Engine stores

### Decision: Evidence folder structure follows existing `aos-run-lifecycle` spec
- **Rationale**: Consistency with existing conventions
- **Structure**: `.aos/evidence/runs/RUN-*/{commands.json,summary.json,logs/,artifacts/}`

## Risks / Trade-offs

- **Risk**: Tight coupling to AOS public API surface → Mitigation: Only use interfaces from `Gmsd.Aos.Public`
- **Risk**: Gating logic becomes complex → Mitigation: Keep rules declarative, test each gate independently
- **Risk**: Run lifecycle events compete with Engine's own event logging → Mitigation: Agents append to separate event stream or use distinct event types

## Migration Plan

No migration needed - this is net-new functionality. Existing code using `IRunRepository` directly will continue to work; new code can opt into `IRunLifecycleManager`.

## Open Questions

1. Should the orchestrator maintain its own event stream separate from Engine's `events.ndjson`?
2. How should phase handlers be registered/discovered? (Delegate registry vs DI scan)
3. What's the retry policy for failed dispatches?

## Context
The Phase Planner workflows sit between the Roadmapper (which produces the roadmap) and the Executor (which executes tasks). The orchestrator already routes to the "Planner" phase via gating when no plan exists, but the actual implementation is missing.

This implementation follows the pattern established by NewProjectInterviewer and Roadmapper in `Gmsd.Agents/Workflows/Planning/`.

## Goals / Non-Goals

**Goals:**
- Decompose roadmap phases into 2-3 atomic, verifiable tasks
- Generate explicit plan.json with file scopes and verification steps
- Capture planning decisions in state for auditability
- Attach assumptions to evidence for later verification
- Integrate seamlessly with existing orchestrator gating

**Non-Goals:**
- Replace existing Interviewer or Roadmapper workflows
- Implement the Executor (task execution is separate)
- Implement the Verifier (verification is separate)
- Support arbitrary numbers of tasks per phase (limit to 2-3 for focus)

## Decisions

### Decision: Three separate components with clear responsibilities
- **ContextGatherer**: Reads specs, collects codebase context, produces brief
- **PhasePlanner**: Takes brief, produces task plans via LLM
- **AssumptionLister**: Extracts and documents assumptions from plans

**Rationale**: Clear separation allows testing each component independently and makes the planning process transparent and debuggable.

### Decision: Use LLM-based planning with structured output
The PhasePlanner will use the existing LLM provider abstractions with structured JSON output schemas for task decomposition.

**Rationale**: Consistent with existing Interviewer and Roadmapper implementations; provides flexibility while maintaining determinism via schema validation.

### Decision: Task count limit (2-3 tasks per phase)
Hard limit of 2-3 atomic tasks per phase to maintain focus and ensure phases are completable.

**Rationale**: Prevents scope creep; aligns with spec-first principle of small, verifiable units of work.

### Decision: Store tasks under `.aos/spec/tasks/{task-id}/`
Each task gets its own directory with task.json (metadata), plan.json (detailed scope), and links.json (relationships).

**Rationale**: Consistent with AOS workspace truth layers; separates spec (intended) from state (operational) and evidence (provable).

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| LLM produces invalid task structures | Schema validation on all outputs; fallback to error state |
| File scopes incorrect (files don't exist) | ContextGatherer validates file existence; assumptions capture uncertainty |
| Too many/few tasks generated | Explicit prompt guidance; post-validation enforces 2-3 task limit |
| Planning takes too long | Set reasonable LLM timeouts; streaming responses where supported |

## Migration Plan
No migration required—this is new functionality. The orchestrator's gating already routes to Planner; this implements the handler that receives that route.

## Open Questions
- Should PhasePlanner support "re-planning" (updating existing task plans)?
- Should we cache context gathering results for multiple planning attempts?

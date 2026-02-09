## Context
The UAT Verifier is part of the Verification & Fix Plane in the agent orchestration layer. It executes after task execution completes, evaluating the work against acceptance criteria defined in the task plan.

The verifier needs to:
1. Read execution evidence from `.aos/evidence/runs/RUN-*/`
2. Evaluate acceptance criteria (initially file checks, build checks, test checks)
3. Produce structured UAT results
4. On failure, create issues and route to FixPlanner for remediation

## Goals
- Provide deterministic, repeatable UAT verification
- Capture structured evidence of pass/fail
- Enable automated fix planning via structured issue creation
- Integrate cleanly with the orchestrator's gating/dispatch system

## Non-Goals
- Complex test frameworks (JUnit, xUnit integration)
- Performance/load testing
- Security/penetration testing
- Manual/human-in-the-loop verification

## Decisions

### Decision: UAT Check Types (Initial)
We support 4 check types to cover common acceptance scenarios:
1. `file-exists` — Verify expected files were created/modified
2. `content-contains` — Verify expected content patterns exist
3. `build-succeeds` — Verify solution builds without errors
4. `test-passes` — Verify specific test methods pass

**Rationale:** Covers the 80% case for task-level acceptance. Can be extended later.

### Decision: Issue Schema Mirrors Task Plan
Issues store:
- `scope` — Files/areas affected
- `repro` — Steps to reproduce the failure
- `expected` — Expected behavior
- `actual` — Actual behavior observed

**Rationale:** Provides FixPlanner with structured context for generating fix plans.

### Decision: Two Artifact Types
1. `UatResult` — Stored in evidence (proof that verification ran)
2. `UatSpec` — Stored in spec (definition of what should be checked)

**Rationale:** Separates "what was checked" (spec layer) from "what was observed" (evidence layer), consistent with AOS truth layers.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Check types too limited | Document extension pattern; add more types as needed |
| False positives in content-contains | Support regex with warnings; encourage specific patterns |
| Build/test check performance | Run async with timeout; fail on timeout as "inconclusive" |
| Issue bloat on repeated failures | De-duplicate by scope+expected hash; update existing issues |

## Migration Plan
N/A — New capability, no existing behavior to migrate.

## Open Questions
- Should we support composite checks (AND/OR logic between checks)? (Defer to v2)
- Should UAT definitions be separate files or embedded in task plans? (Start embedded, extract if needed)

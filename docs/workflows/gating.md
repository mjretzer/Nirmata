# Gating Rules & Workflow State Machine

Source: Control_Plane__Delegation.pdf (Section 1), AOS_CLI_Commands.pdf (Section 12)

The Nirmata/AOS engine enforces **spec-first sequencing**: you cannot execute without a plan, and you cannot plan without a roadmap and spec. This document is the authoritative reference for those rules.

---

## Core Gate: Orchestrator State Machine

On every invocation the Orchestrator evaluates this gate in order:

```
1. spec/project.json missing?
   └─► Route to: New-Project Interviewer  (aos → new-project)

2. spec/roadmap.json missing?
   └─► Route to: Roadmapper  (create-roadmap)

3. No spec/tasks/TSK-*/plan.json for target phase?
   └─► Route to: Phase Planner  (plan-phase PH-####)

4. Plan exists, not executed?
   └─► Route to: Task Executor  (execute-plan)

5. Execution done, not verified?
   └─► Route to: UAT Verifier  (verify-work PH-####)

6. Verification failed?
   └─► Route to: Fix Planner  (plan-fix)
         └─► back to Task Executor  (execute-plan)
               └─► back to UAT Verifier  (verify-work)

7. Verification passed, more phases remain?
   └─► Route to: Phase Planner  (plan-phase PH-####)  [next phase]

8. All phases done for milestone?
   └─► Route to: Milestone Manager  (complete-milestone → new-milestone)
```

---

## Gating Invariants

| Rule | Enforced By |
|---|---|
| No `execute-plan` without `plan.json` | Orchestrator pre-flight |
| No `plan-phase` without `roadmap.json` | Orchestrator pre-flight |
| No `plan-phase` without `project.json` | Orchestrator pre-flight |
| Max 3 steps per task plan | Subagent Orchestrator dispatch |
| Task executor writes only to `allowedFiles` | Context pack `allowedScope` |
| One task = one commit | Atomic Git Committer |
| Verification must produce explicit pass/fail | UAT Verifier (no implicit "done") |
| Fix plans reference UAT artifact, not chat | Fix Planner loads `uat.json` / `ISS-*.json` |
| State transitions recorded in `events.ndjson` | State Manager (append-only) |
| All produced artifacts schema-valid | `aos validate spec` post-write |

---

## Full Workflow: Greenfield Project

```
aos init
  → Workspace created

aos (freeform or new-project intent)
  → New-Project Interviewer
  → .aos/spec/project.json ✓

create-roadmap
  → Roadmapper
  → .aos/spec/roadmap.json ✓
  → .aos/state/state.json initialized ✓

[Optional: discuss-phase PH-0001]
  → Phase Context Gatherer
  → phase brief → .aos/state/state.json (decisions) ✓

[Optional: list-phase-assumptions PH-0001]
  → Phase Assumption Lister
  → assumptions snapshot → .aos/evidence/runs/RUN-*/ ✓

[Optional: research-phase PH-0001]
  → Phase Researcher
  → .aos/context/packs/PH-0001-research.json ✓

plan-phase PH-0001
  → Phase Planner
  → .aos/spec/tasks/TSK-000001/plan.json ✓ (2–3 tasks)
  → .aos/spec/tasks/TSK-000002/plan.json ✓

execute-plan
  → [For each TSK in phase]
      → Context Engineer: build .aos/context/packs/TSK-######.json
      → Subagent Orchestrator: spawn fresh subagent
      → Task Executor: apply changes to allowedFiles
      → Run verification commands
      → Atomic Git Committer: one commit per task
      → State Manager: advance cursor
  → All tasks done ✓

verify-work PH-0001
  → UAT Verifier
  → Check each acceptance criterion
  → IF PASS:
      → .aos/state/state.json cursor = verified-pass ✓
      → History Writer: update narrative summary ✓
      → Proceed to plan-phase PH-0002
  → IF FAIL:
      → .aos/spec/issues/ISS-*.json ✓
      → .aos/spec/uat/UAT-*.json ✓
      → Route to Fix Planner

[If FAIL] plan-fix
  → Fix Planner
  → .aos/spec/tasks/TSK-######/plan.json (fix tasks, max 3) ✓
  → execute-plan → verify-work [repeat until pass]

plan-phase PH-0002
  → [continue loop…]

[When all phases done] discuss-milestone
  → Milestone Context Gatherer
  → Capture next-milestone intent ✓

complete-milestone
  → Milestone Manager
  → Marks MS-0001 shipped ✓

new-milestone v2
  → Milestone Creator
  → .aos/spec/milestones/MS-0002/milestone.json ✓
  → .aos/spec/phases/PH-####/phase.json ✓ (initial phases)
  → [return to plan-phase loop]
```

---

## Full Workflow: Brownfield (Existing Repo)

```
aos init
  → Workspace created

map-codebase  (aos codebase scan)
  → Codebase Mapper Agent
  → .aos/codebase/{map,stack,architecture,structure,
                    conventions,testing,integrations,concerns}.json ✓
  → .aos/codebase/cache/{symbols,file-graph}.json ✓
  → aos validate codebase ✓

new-project  (grounded in codebase map)
  → New-Project Interviewer
  → Questions focused on incremental change, not re-discovery
  → .aos/spec/project.json ✓

[continue as greenfield from create-roadmap onward]
```

---

## Pause & Resume Workflow

```
[Mid-execution, any point]
pause-work
  → Pause/Resume Manager
  → .aos/state/handoff.json written:
      { cursor, in-flight task/step, allowed scope, pending verification, next command }
  → .aos/state/events.ndjson: work.paused ✓
  → Session ends safely

[New session, hours/days later]
resume-work
  → Pause/Resume Manager
  → Load .aos/state/handoff.json
  → Confirm matches current spec/roadmap cursor
  → Rebuild minimal context pack
  → .aos/state/events.ndjson: work.resumed ✓
  → Continue from exact position

[Resume specific interrupted subagent]
resume-task <EXECUTION-ID>
  → Locate .aos/evidence/runs/RUN-*/
  → Restore execution packet + context pack
  → Dispatch subagent continuation
```

---

## Reprioritization Workflow

```
[At any point with future phases]

add-phase  (append new phase at end)
  → Roadmap Modifier
  → .aos/spec/roadmap.json updated ✓

insert-phase PH-INDEX  (insert before a specific phase)
  → Roadmap Modifier
  → Subsequent phases renumbered ✓
  → .aos/state/state.json cursor pointers reconciled ✓

remove-phase PH-INDEX  (only future phases)
  → Phase Remover  [OR Roadmap Modifier]
  → Safety check: not active/in-progress phase
  → Subsequent phases renumbered ✓

[After any roadmap edit]
  → aos validate spec
  → aos validate state
  → aos event append roadmap.modified <filejson>
  → Route to: discuss-phase PH-#### → plan-phase PH-#### → execute-plan
```

---

## Backlog / Todo Workflow

```
[At any point, without interrupting flow]
add-todo "implement dark mode toggle"
  → Todo Capturer
  → .aos/context/todos/TODO-001.json ✓
  → Returns to prior mode immediately

[When ready to review]
check-todos [area]
  → Todo Reviewer & Selector
  → Filtered list presented
  → User selects one
  → Route: aos spec task create … (OR insert-phase)
  → Returns to plan/execute flow

[Issue triage session]
consider-issues
  → Deferred Issues Curator
  → Load .aos/spec/issues/**
  → Mark resolved, flag urgent, classify deferred
  → If urgent → route to roadmap modification or fix planning
```

---

## Verification Pass/Fail Decision Tree

```
verify-work PH-#### (or PLAN, or TSK-…)
  │
  ├─► Load acceptance criteria from task plans
  ├─► Open RUN-* verification record
  ├─► Prompt through each check
  │
  ├─► ALL PASS?
  │     └─► cursor status = verified-pass
  │         History Writer updates narrative
  │         → advance to next phase or milestone
  │
  └─► ANY FAIL?
        └─► Create ISS-*.json per finding
            Write UAT-*.json with full observations
            cursor status = verified-fail
            → plan-fix
                └─► Fix Planner selects 2–3 smallest fix tasks
                    Writes TSK-*/plan.json with UAT-referenced verification
                    → execute-plan
                          └─► verify-work (same checks)
                                └─► [repeat until pass]
```

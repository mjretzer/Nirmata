# Backlog Capture & Triage Agents

Source: Backlog_Capture__Triage.pdf (Section 9)

---

## Deferred Issues Curator Agent

### Responsibilities

- Review and triage deferred enhancements/bugs tracked across sessions (`.aos/spec/issues/**` and/or a consolidated issues index).
- Close issues already addressed and prevent "ghost backlog" accumulation outside the main roadmap/plan loop.
- Identify urgent or blocking issues and route them back into the primary workflow (roadmap modification, fix planning, or immediate execution gating).
- Keep prioritization consistent with current cursor, constraints, and milestone intent from `.aos/state/state.json`.

### Step Format (single run)

1. Receive `consider-issues` intent (CLI equivalent).
2. Load current position/constraints from `.aos/state/state.json` and load the issue backlog from `.aos/spec/issues/**` (plus any issues index file if used).
3. Triage issues: mark resolved, flag urgent/blocking, and classify remaining as deferred (with a brief rationale).
4. Persist updates: update issue records (status, priority, tags), append an event (`aos event append issues.triaged <filejson>`), and if urgent, create a routing recommendation (insert phase, add phase, or create a fix task).
5. Hand off to Orchestrator with the routed next action: roadmap modification / fix-plan / plan-phase, or "no action; remain deferred."

### Summary

Deferred Issues Curator is the backlog hygiene workflow that keeps out-of-band work from silently accumulating. It loads the persisted issue backlog and current constraints, closes items that are already resolved, elevates urgent or blocking issues, and persists triage decisions back into issue specs and state events. When an item becomes urgent, it explicitly routes it into the main roadmap/plan loop (via phase insertion/addition or fix planning) so prioritization remains coherent and auditable.

---

## Todo Capturer Agent

### Responsibilities

- Capture ad-hoc ideas/tasks without interrupting the current planning/execution/verification flow.
- Normalize the user's description into a concise, actionable todo label while preserving intent.
- Persist todos into a distinct "later" queue (separate from roadmap/plan) so they can be reviewed and routed later.
- Return control to the current workflow mode without forcing re-plan.

### Step Format (single run)

1. Receive `add-todo <desc>` intent (CLI equivalent).
2. Normalize the description into a concise todo entry (title + optional tags/area + captured-from cursor).
3. Persist the todo into the backlog queue (write a todo record under `.aos/context/todos/TODO-*.json` or append to a configured todos index) and append an event (`aos event append todo.added <filejson>`).
4. Return the updated todo id/reference and hand control back to Orchestrator with "resume prior mode" (no cursor change unless explicitly requested).

### Summary

Todo Capturer is the capture lane that prevents valuable ideas from being lost mid-flight. It records a normalized todo item into a dedicated deferred queue (separate from roadmap and task plans), logs the capture as an event for traceability, and immediately returns control to the current workflow so progress continues uninterrupted.

---

## Todo Reviewer & Selector Agent

### Responsibilities

- Retrieve and list pending captured todos from the deferred queue.
- Support optional filtering by area/workstream to keep reviews focused.
- Present a clean selection set and capture the user's chosen todo without derailing the roadmap.
- Route the selected todo into the standard planning/execution loop (as a new task, a phase insertion, or a fix item) while preserving continuity.

### Step Format (single run)

1. Receive `check-todos [area]` intent (CLI equivalent).
2. Load the pending todo backlog from `.aos/context/todos/**` (and any todos index file if used).
3. Apply optional area/tag filter and sort by recency/priority signals (if present).
4. Present the filtered todo options and record the selected todo id as the next focus item (selection is explicit, not implied).
5. Hand off to Orchestrator with a routing recommendation: convert the selected todo into a spec task (`aos spec task create …`) or roadmap change (insert/add phase), then proceed with `discuss-phase`/`plan-phase` or direct task planning as appropriate.

### Summary

Todo Reviewer & Selector is the retrieval and routing counterpart to Todo Capturer. It loads the deferred todo queue, optionally filters by area, presents the pending items for deliberate selection, and then routes the chosen todo back into the primary plan/execute workflow in a controlled way — typically by converting it into a task spec or a roadmap insertion — so deferred work re-enters the system without disrupting long-horizon structure.

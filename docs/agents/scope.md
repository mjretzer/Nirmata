# Scope Management Agents

Source: Scope_Managment.pdf (Section 5)

---

## Roadmap Modifier Agent

### Responsibilities

- Enable modular roadmap edits (add/insert/remove phases) without rebuilding the entire planning system.
- Preserve long-horizon coherence by maintaining correct phase ordering and stable references (including renumbering when required).
- Support reprioritization while preserving continuity with the standard loop (plan → execute → verify).
- Prevent drift by ensuring roadmap edits are consistent with current cursor and persisted state.

### Step Format (single run)

1. Receive command intent: `add-phase`, `insert-phase <PH-INDEX>`, or `remove-phase <PH-INDEX>` (CLI equivalent).
2. Load canonical planning context: `.aos/spec/roadmap.json` plus current cursor/constraints from `.aos/state/state.json` (and milestone/phase specs as needed).
3. Apply the modification to the roadmap structure: append, insert (shift later phases), or remove (only future phases unless explicitly overridden) and renumber/reindex subsequent phases consistently.
4. Persist updates: write revised `.aos/spec/roadmap.json` and adjust any dependent references (phase ids, milestone links, and state cursor pointers) so the current position remains valid.
5. Validate (`aos validate spec` + `aos validate state`), append an event (`aos event append roadmap.modified <filejson>`), and attach evidence in `.aos/evidence/runs/RUN-*/`.
6. Hand off to Orchestrator with the next action: `discuss-phase <PH-####>` (optional) → `plan-phase <PH-####>` → `execute-plan` under the updated ordering.

### Summary

Roadmap Modifier is the reprioritization mechanism that edits the long-horizon plan in-place. It supports adding new phases, inserting urgent work at a specific position, or removing future phases while renumbering and updating references so the roadmap remains coherent. After persisting the revised `.aos/spec/roadmap.json` and keeping `.aos/state/state.json` cursor pointers valid, it routes cleanly back into the normal plan/execute workflow without forcing a restart.

---

## Phase Remover Agent

### Responsibilities

- Remove a future phase cleanly without leaving dangling references or ambiguous "what's next."
- Renumber/reindex subsequent phases to keep roadmap ordering coherent and modular.
- Preserve continuity by ensuring the current cursor in `.aos/state/state.json` remains valid after the edit.
- Validate that removal is safe (not removing the active/in-progress phase unless explicitly forced).

### Step Format (single run)

1. Receive `remove-phase <PH-INDEX>` intent (CLI equivalent).
2. Load canonical planning state: `.aos/spec/roadmap.json` (phase list + ordering) and `.aos/state/state.json` (current position/cursor).
3. Validate safety: confirm the target phase is **not** the current in-progress phase (and that no active tasks/plans are bound to it); otherwise emit a blocker/issue and stop.
4. Remove the phase from the roadmap structure and renumber/reindex subsequent phases to eliminate gaps (update ordering, ids/aliases as spec requires).
5. Persist updates: write the revised `.aos/spec/roadmap.json`, reconcile any dependent links (milestone ↔ phase, tasks ↔ phase), and adjust `.aos/state/state.json` pointers if the cursor references shifted indices.
6. Validate (`aos validate spec` + `aos validate state`), append an event (`aos event append phase.removed <filejson>`), and attach evidence in `.aos/evidence/runs/RUN-*/`, then return to the normal loop.

### Summary

Phase Remover implements `remove-phase` as a safe roadmap cleanup operator. It deletes a future phase from the roadmap, renumbers what follows to preserve coherent ordering, and reconciles operational cursor pointers so the system remains resumable. By validating removal safety, persisting changes into `.aos/spec/**` and `.aos/state/**`, and recording an auditable event/evidence trail, it keeps the long-horizon plan modular without forcing a restart.

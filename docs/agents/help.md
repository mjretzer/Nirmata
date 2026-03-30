# Help / Usage Guide Agent

Source: Help.pdf (Section 10)

---

## Responsibilities

- Serve as the authoritative command index for the framework (what commands exist, what they do, and when to use them).
- Provide lightweight routing guidance so the operator can pick the correct next command without guessing.
- Reflect the current CLI surface (`AOS: aos --help` and command group help) as the source of truth, not tribal knowledge.
- Keep usage guidance consistent with gating rules (spec → roadmap → plan → execute → verify → fix).

## Step Format (single run)

1. Receive `help` intent (CLI equivalent).
2. Render the command catalog by pulling the current command set from the CLI (`aos --help` plus group help like `aos spec --help`, `aos run --help`, `aos validate --help`, `aos codebase --help`, `aos pack --help`).
3. Present a compact "what to run next" guide keyed to common states:
   - Missing project spec
   - Missing roadmap
   - Ready to plan
   - Ready to execute
   - Verification failed
   - Etc.
4. Return control to the operator with **no state changes** and **no evidence writes** (help is read-only by default).

## Summary

Help / Usage Guide Agent is the system's quick-reference surface. On help, it prints the current command catalog and brief routing guidance derived from the actual CLI help output, enabling the operator to select the correct next command through the long-horizon workflow without relying on memory or ad hoc interpretation.

---

## Quick Routing Guide

| Current State | Next Command |
|---|---|
| No `.aos/` workspace yet | `aos init` |
| `.aos/` exists, no `spec/project.json` | Trigger **New-Project Interviewer** |
| `project.json` exists, no `roadmap.json` | `aos` → **Roadmapper** (`create-roadmap`) |
| Roadmap exists, no task plans for phase | `plan-phase PH-####` |
| Task plans exist, not executed | `execute-plan` |
| Execution done, not verified | `verify-work PH-####` |
| Verification failed | `plan-fix` → `execute-plan` → `verify-work` |
| Phase complete, next phase exists | `plan-phase PH-####` (next) |
| All phases done, next milestone | `discuss-milestone` → `new-milestone` |
| Want to reprioritize | `add-phase` / `insert-phase` / `remove-phase` |
| Mid-session interrupt | `pause-work` |
| Resuming after interrupt | `resume-work` |
| Unsure where you are | `aos status` / **Progress Reporter** |
| Want to capture idea without interrupting | `add-todo <desc>` |
| Want to review captured ideas | `check-todos [area]` |
| Existing repo, no codebase map yet | `map-codebase` (`aos codebase scan`) |

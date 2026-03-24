# AOS Workspace Directory Layout

Source: AOS_Directory_Layout.pdf (Section 11)

---

## Full Directory Tree

```
.aos/
├── schemas/                          # JSON schema validation contracts
│   ├── project.schema.json
│   ├── roadmap.schema.json
│   ├── milestone.schema.json
│   ├── phase.schema.json
│   ├── task.schema.json
│   ├── uat.schema.json
│   ├── issue.schema.json
│   ├── event.schema.json
│   ├── context-pack.schema.json
│   └── evidence.schema.json
│
├── spec/                             # INTENDED TRUTH — the plan
│   ├── project.json
│   ├── roadmap.json
│   ├── milestones/
│   │   └── MS-0001/
│   │       ├── milestone.json
│   │       └── index.json
│   ├── phases/
│   │   └── PH-0001/
│   │       ├── phase.json
│   │       ├── assumptions.json
│   │       ├── research.json
│   │       └── index.json
│   ├── tasks/
│   │   └── TSK-000001/
│   │       ├── task.json
│   │       ├── plan.json
│   │       ├── uat.json
│   │       └── links.json
│   ├── issues/
│   │   └── ISS-0001.json
│   └── uat/
│       └── UAT-0001.json
│
├── state/                            # OPERATIONAL TRUTH — current cursor
│   ├── state.json
│   ├── events.ndjson
│   ├── handoff.json                  # Written by pause-work
│   └── checkpoints/
│       └── 2026-01-13T021500Z.json
│
├── evidence/                         # PROVABLE TRUTH — what was executed
│   ├── runs/
│   │   └── RUN-2026-01-13T021500Z/
│   │       ├── summary.json
│   │       ├── commands.json
│   │       ├── logs/
│   │       │   ├── build.log
│   │       │   └── test.log
│   │       └── artifacts/
│   ├── task-evidence/
│   │   └── TSK-000001/
│   │       ├── latest.json
│   │       └── history/
│   │           └── RUN-2026-01-13T021500Z.json
│   └── last-run.json
│
├── codebase/                         # REPO INTELLIGENCE
│   ├── map.json
│   ├── stack.json
│   ├── architecture.json
│   ├── structure.json
│   ├── conventions.json
│   ├── testing.json
│   ├── integrations.json
│   ├── concerns.json
│   └── cache/
│       ├── symbols.json
│       └── file-graph.json
│
├── context/                          # PACKAGING LAYER — deterministic packs
│   ├── packs/
│   │   ├── TSK-000001.json
│   │   └── PH-0001.json
│   ├── todos/
│   │   └── TODO-*.json
│   └── templates/
│       └── task-pack.template.json
│
├── cache/                            # NON-AUTHORITATIVE OPS SUPPORT
│   ├── locks/
│   └── tmp/
```

---

## Layer Descriptions

### `schemas/` — Validation Contracts

Defines the validation contracts for the AOS workspace. Makes every AOS artifact structurally enforceable so the CLI can reliably validate inputs, reject malformed or incomplete data, and guarantee that downstream commands operate on predictable shapes. This turns the system into a "programmable spec/state engine" rather than a loose set of JSON files — because correctness can be checked, not assumed.

### `spec/` — Intended Truth

The system's intended-truth layer. Captures the plan — what the project is, what work exists, how work is organized, and what "done" means — independent of runtime conditions. The CLI reads `spec/` to decide what should happen next and to evaluate progress against defined objectives, without relying on memory or ad hoc interpretation.

### `state/` — Operational Truth

The operational-truth layer. Records the current cursor of execution (what is active, what stage you're in, what is blocked) and the chronological trail of state transitions that led there. Enables resumability, crash recovery, deterministic "where are we?" answers, and controlled state transitions without rewriting the plan or conflating intent with reality.

### `evidence/` — Provable Truth

The provable-truth layer. Stores an auditable record of what was actually executed and what outputs were produced, so claims about progress are grounded in artifacts rather than narration. Supports reproducibility, debugging, accountability, and "show me the proof" workflows, while keeping execution history separate from both planning and current-state cursors.

### `codebase/` — Repository Intelligence

Encodes how the codebase is structured and how it should be worked on — so the CLI/agent can make correct, consistent decisions with minimal context loading. Prevents repeated rediscovery of architecture, conventions, and build/test mechanics, and reduces the chance of changes that violate project standards.

### `context/` — Packaging Layer

Assembles deterministic, reusable "work packets" that contain exactly the information needed to execute a unit of work (task/phase) without bloating the session context window. Makes execution more consistent, reduces drift between runs, and supports automation that can be inspected and regenerated.

### `cache/` — Non-Authoritative Operational Support

Holds ephemeral or derivative data that improves performance and safety (e.g., preventing concurrent mutation, enabling quick resume pointers), without contaminating the system's source-of-truth layers. Anything here should be disposable and regenerable without affecting correctness — only speed and convenience.

---

## Key Files Quick Reference

| File | Layer | Purpose |
|---|---|---|
| `.aos/spec/project.json` | Intended truth | Project goals, constraints, success criteria |
| `.aos/spec/roadmap.json` | Intended truth | Full milestone/phase structure |
| `.aos/spec/tasks/TSK-*/plan.json` | Intended truth | Atomic task execution plan |
| `.aos/state/state.json` | Operational truth | Current cursor: milestone/phase/task/status |
| `.aos/state/events.ndjson` | Operational truth | Append-only event log |
| `.aos/state/handoff.json` | Operational truth | Pause/resume snapshot |
| `.aos/evidence/runs/RUN-*/` | Provable truth | Per-run logs, commands, artifacts |
| `.aos/evidence/task-evidence/TSK-*/latest.json` | Provable truth | Latest evidence for a task |
| `.aos/codebase/map.json` | Repo intelligence | High-level repo overview |
| `.aos/codebase/conventions.json` | Repo intelligence | Coding standards |
| `.aos/context/packs/TSK-*.json` | Packaging | Bounded context pack for a task |
| `.aos/context/todos/TODO-*.json` | Packaging | Deferred todo items |

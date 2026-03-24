# AOS Artifact Schemas & Contracts

Source: AOS_Directory_Layout.pdf (Section 11), Component_Diagram.pdf (Section 13)

This document describes the schema files embedded in `Gmsd.Aos/Resources/Schemas/` and the structural contracts they enforce.

---

## Schema Files

| Schema File | Validates | Key Fields |
|---|---|---|
| `project.schema.json` | `.aos/spec/project.json` | goals, constraints, non-goals, success criteria, environment, integrations, security, timeline |
| `roadmap.schema.json` | `.aos/spec/roadmap.json` | milestones array, phases array, task placeholders, ordering |
| `milestone.schema.json` | `.aos/spec/milestones/MS-####/milestone.json` | id (MS-####), title, status, phases[], closure criteria |
| `phase.schema.json` | `.aos/spec/phases/PH-####/phase.json` | id (PH-####), milestone ref, title, outcomes, task refs |
| `task.schema.json` | `.aos/spec/tasks/TSK-######/task.json` | id (TSK-######), phase ref, milestone ref, title, status |
| `task.schema.json` (plan) | `.aos/spec/tasks/TSK-######/plan.json` | taskId, phaseId, milestoneId, allowedFiles[], steps[] (max 3), verificationCommands[], definitionOfDone[] |
| `uat.schema.json` | `.aos/spec/tasks/TSK-*/uat.json`, `.aos/spec/uat/UAT-####.json` | task ref, acceptance checks, pass/fail status, observations, repro steps |
| `issue.schema.json` | `.aos/spec/issues/ISS-####.json` | id (ISS-####), severity, scope, repro, expected vs actual, impacted files, status |
| `event.schema.json` | `.aos/state/events.ndjson` (each line) | type, timestamp, payload, references |
| `context-pack.schema.json` | `.aos/context/packs/*.json` | target (TSK/PH id), mode, artifact list (ordered, with file paths), budget |
| `evidence.schema.json` | `.aos/evidence/runs/RUN-*/summary.json` | run id, task ref, status (pass/fail), commands, logs, artifacts |

---

## Task Plan Contract (Most Critical)

The task plan (`plan.json`) is the atomic unit of work. It must include:

```json
{
  "taskId": "TSK-000013",
  "phaseId": "PH-0002",
  "milestoneId": "MS-0001",
  "title": "Short human-readable description",
  "allowedFiles": [
    "src/Gmsd.Agents/Workflows/Execution/TaskExecutor/TaskExecutorWorkflow.cs",
    "tests/..."
  ],
  "steps": [
    {
      "index": 1,
      "action": "Description of exactly what to do",
      "targetFile": "src/...",
      "details": "Specific implementation notes"
    }
  ],
  "verificationCommands": [
    "dotnet build src/",
    "dotnet test tests/Gmsd.Agents.Tests/ --filter Category=TaskExecutor"
  ],
  "definitionOfDone": [
    "Build passes",
    "Targeted tests pass",
    "No regressions in related tests"
  ]
}
```

**Constraints:**
- Max **3 steps** per plan (enforced by Subagent Orchestrator)
- `allowedFiles` is a strict scope list — task executor may only touch these files
- `verificationCommands` must be runnable commands (not descriptions)

---

## State JSON Contract

`.aos/state/state.json` tracks the three core domains:

```json
{
  "position": {
    "milestoneId": "MS-0001",
    "phaseId": "PH-0002",
    "taskId": "TSK-000013",
    "stepIndex": 1,
    "status": "InProgress"
  },
  "decisions": [
    {
      "id": "DEC-001",
      "topic": "Use PostgreSQL for persistence",
      "decision": "Use EF Core + PostgreSQL",
      "rationale": "Team familiarity",
      "timestamp": "2026-01-13T02:15:00Z"
    }
  ],
  "blockers": [
    {
      "id": "BLK-001",
      "description": "Waiting for API key from infra team",
      "affectedTask": "TSK-000015",
      "timestamp": "2026-01-13T02:15:00Z"
    }
  ],
  "lastTransition": {
    "from": "planned",
    "to": "InProgress",
    "timestamp": "2026-01-13T02:15:00Z",
    "trigger": "execute-plan"
  }
}
```

---

## Event Types (`.aos/state/events.ndjson`)

Each line is a JSON object conforming to `event.schema.json`.

| Event Type | Emitted By | When |
|---|---|---|
| `project.created` | New-Project Interviewer | After `project.json` written |
| `roadmap.created` | Roadmapper | After `roadmap.json` written |
| `phase.planned` | Phase Planner | After task plans written for a phase |
| `phase.intent.captured` | Phase Context Gatherer | After `discuss-phase` completes |
| `phase.assumptions.listed` | Phase Assumption Lister | After assumptions snapshot written |
| `phase.removed` | Phase Remover | After phase removed from roadmap |
| `roadmap.modified` | Roadmap Modifier | After add/insert/remove phase |
| `milestone.created` | Milestone Creator | After new milestone + phases written |
| `milestone.completed` | Milestone Manager | After milestone marked shipped |
| `milestone.intent.captured` | Milestone Context Gatherer | After `discuss-milestone` completes |
| `task.resumed` | Pause/Resume Manager | After `resume-task <id>` |
| `work.paused` | Pause/Resume Manager | After handoff snapshot written |
| `work.resumed` | Pause/Resume Manager | After context rebuilt from handoff |
| `uat.completed` | UAT Verifier | After verification run recorded |
| `fix.planned` | Fix Planner | After fix task plans written |
| `issues.triaged` | Deferred Issues Curator | After backlog triage complete |
| `todo.added` | Todo Capturer | After todo persisted to queue |
| `history.written` | History Writer | After narrative summary updated |

---

## Context Pack Contract

`.aos/context/packs/<TSK|PH>-*.json` — fed to every subagent run:

```json
{
  "packId": "TSK-000013",
  "mode": "execute",
  "budgetTokens": 8000,
  "artifacts": [
    {
      "order": 1,
      "path": ".aos/spec/project.json",
      "role": "project-vision"
    },
    {
      "order": 2,
      "path": ".aos/state/state.json",
      "role": "current-position"
    },
    {
      "order": 3,
      "path": ".aos/spec/tasks/TSK-000013/plan.json",
      "role": "task-plan"
    }
  ],
  "allowedScope": [
    "src/Gmsd.Agents/Workflows/Execution/TaskExecutor/TaskExecutorWorkflow.cs"
  ]
}
```

**Rules:**
- Artifacts are ordered (lower = higher priority if budget exceeded)
- Only files that actually exist on disk may be referenced
- `allowedScope` is authoritative — subagent must not write outside these paths

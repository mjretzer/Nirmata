# Agent Routing Map

Quick reference: which agent or workflow handles each command or intent.

---

## CLI Command → Agent Mapping

| Command / Intent | Agent | Output Artifacts |
|---|---|---|
| `aos init` | Workspace / Orchestrator setup | `.aos/` directory created |
| `aos status` | Progress Reporter | Console output (read-only) |
| `help` / `aos help` | Help / Usage Guide Agent | Console output (read-only) |
| `new-project` | New-Project Interviewer | `.aos/spec/project.json` |
| `create-roadmap` | Roadmapper | `.aos/spec/roadmap.json`, `.aos/state/state.json` |
| `discuss-phase PH-####` | Phase Context Gatherer | `.aos/state/state.json` (decisions), event |
| `list-phase-assumptions PH-####` | Phase Assumption Lister | `.aos/evidence/runs/RUN-*/` (snapshot) |
| `research-phase PH-####` | Phase Researcher | `.aos/context/packs/PH-####-research.json` |
| `plan-phase PH-####` | Phase Planner | `.aos/spec/tasks/TSK-*/plan.json` (2–3 tasks) |
| `execute-plan` | Task Executor + Subagent Orchestrator | Source code changes, `.aos/evidence/runs/RUN-*/` |
| *(after each task)* | Atomic Git Committer | Git commit, `.aos/evidence/task-evidence/TSK-*/latest.json` |
| `verify-work PH-####` | UAT Verifier | `.aos/spec/issues/ISS-*.json`, `.aos/spec/uat/UAT-*.json` |
| `plan-fix` | Fix Planner | `.aos/spec/tasks/TSK-*/plan.json` (fix tasks) |
| `map-codebase` | Codebase Mapper Agent | `.aos/codebase/**`, `.aos/codebase/cache/**` |
| `pause-work` | Pause/Resume Manager | `.aos/state/handoff.json` |
| `resume-work` | Pause/Resume Manager | Context rebuilt from handoff |
| `resume-task <id>` | Pause/Resume Manager | Subagent continuation |
| `add-phase` | Roadmap Modifier | `.aos/spec/roadmap.json` (updated) |
| `insert-phase PH-INDEX` | Roadmap Modifier | `.aos/spec/roadmap.json` (updated, renumbered) |
| `remove-phase PH-INDEX` | Phase Remover / Roadmap Modifier | `.aos/spec/roadmap.json` (updated, renumbered) |
| `discuss-milestone` | Milestone Context Gatherer | `.aos/state/state.json` (decisions), event |
| `complete-milestone` | Milestone Manager | Milestone status = shipped, event |
| `new-milestone [name]` | Milestone Creator | `.aos/spec/milestones/MS-*/`, `.aos/spec/phases/PH-*/` |
| `add-todo <desc>` | Todo Capturer | `.aos/context/todos/TODO-*.json` |
| `check-todos [area]` | Todo Reviewer & Selector | Routing recommendation to Orchestrator |
| `consider-issues` | Deferred Issues Curator | `.aos/spec/issues/ISS-*.json` (updated) |
| *(after task/plan completes)* | History Writer | `.aos/spec/summary.md` or `.aos/evidence/summary.md`, `.aos/state/state.json` |
| *(any state transition)* | State Manager | `.aos/state/state.json`, `.aos/state/events.ndjson` |
| *(any agent run)* | Context Engineer | `.aos/context/packs/<TSK\|PH>-*.json` |

---

## Agent → Workflow Class Mapping

| Agent | Namespace / Path in `Gmsd.Agents` |
|---|---|
| Orchestrator | `Workflows/ControlPlane/Orchestrator/` |
| Subagent Orchestrator | `Workflows/ControlPlane/SubagentRuns/` |
| Pause/Resume Manager | `Workflows/ControlPlane/Continuity/` |
| New-Project Interviewer | `Workflows/Planning/NewProjectInterviewer/` |
| Roadmapper | `Workflows/Planning/Roadmapper/` |
| Phase Planner | `Workflows/Planning/PhasePlanner/` |
| Codebase Mapper Agent | `Workflows/Brownfield/CodebaseMapper/` |
| Task Executor | `Workflows/Execution/TaskExecutor/` |
| Atomic Git Committer | `Workflows/Execution/AtomicGitCommitter/` |
| UAT Verifier | `Workflows/Verification/UatVerifier/` |
| Fix Planner | `Workflows/Verification/FixPlanner/` |

> **Note:** The following agents are additional workflow classes not yet shown in the tree above. Their planned locations:
> - Phase Context Gatherer → `Workflows/Planning/PhasePlanner/PhaseContextGatherer/`
> - Phase Assumption Lister → `Workflows/Planning/PhasePlanner/PhaseAssumptionLister/`
> - Phase Researcher → `Workflows/Planning/PhasePlanner/PhaseResearcher/`
> - History Writer → `Workflows/ControlPlane/Continuity/HistoryWriter/`
> - Progress Reporter → `Workflows/ControlPlane/Continuity/ProgressReporter/`
> - Milestone Manager → `Workflows/Planning/MilestoneManager/`
> - Milestone Creator → `Workflows/Planning/MilestoneManager/MilestoneCreator/`
> - Milestone Context Gatherer → `Workflows/Planning/MilestoneManager/MilestoneContextGatherer/`
> - Roadmap Modifier → `Workflows/Planning/RoadmapModifier/`
> - Phase Remover → `Workflows/Planning/RoadmapModifier/PhaseRemover/`
> - Deferred Issues Curator → `Workflows/ControlPlane/Backlog/DeferredIssuesCurator/`
> - Todo Capturer → `Workflows/ControlPlane/Backlog/TodoCapturer/`
> - Todo Reviewer & Selector → `Workflows/ControlPlane/Backlog/TodoReviewer/`
> - Help / Usage Guide → `Workflows/ControlPlane/HelpGuide/`

---

## Plane → Namespace Mapping

| Plane | Namespace Root |
|---|---|
| Control Plane | `Workflows/ControlPlane/` |
| Planning Plane | `Workflows/Planning/` |
| Brownfield Plane | `Workflows/Brownfield/` |
| Execution Plane | `Workflows/Execution/` |
| Verification & Fix Plane | `Workflows/Verification/` |
| Continuity Plane | `Workflows/ControlPlane/Continuity/` |

---

## Truth Layer → Service Class Mapping

| Truth Layer | `Gmsd.Aos` Service |
|---|---|
| Intended truth (`.aos/spec/`) | `Engine/Spec/` |
| Operational truth (`.aos/state/`) | `Engine/State/` |
| Provable truth (`.aos/evidence/`) | `Engine/Evidence/` |
| Repo intelligence (`.aos/codebase/`) | `Engine/Codebase/` |
| Context packs (`.aos/context/`) | `Engine/Context/` |
| Validation contracts (`.aos/schemas/`) | `Engine/Schemas/` + `Engine/Validation/` |
| File resolution | `Engine/Paths/` |
| JSON read/write | `Engine/Serialization/` |
| Checkpoint snapshots | `Engine/Checkpoints/` |
| Cache/lock hygiene | `Engine/Maintenance/` |
| Import/export | `Engine/ImportExport/` |

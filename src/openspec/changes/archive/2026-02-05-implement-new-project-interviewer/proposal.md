# Change: Implement New-Project Interviewer Workflow

## Why

PH-PLN-0003 from the roadmap requires a planning-plane workflow that conducts structured interviews to gather project requirements when no `.aos/spec/project.json` exists. The gating engine already routes to "Interviewer" phase for missing projects, but no handler implementation exists. Without this workflow, the orchestrator cannot progress past the first gate in greenfield scenarios.

## What Changes

- Add `nirmata.Agents/Execution/Planning/NewProjectInterviewer/` namespace with:
  - `INewProjectInterviewer` interface defining the interview contract
  - `NewProjectInterviewer` implementation conducting LLM-driven interviews
  - `InterviewSession` model for tracking Q&A state
  - `ProjectSpecGenerator` for normalizing interview output to `project.json` schema
- Interview evidence capture: transcript and summary written to `.aos/evidence/runs/RUN-*/artifacts/`
- Integration with `IRunLifecycleManager` to attach interview artifacts to the current run
- Validation gate: `validate spec` passes after `project.json` is written

## Impact

- **Affected specs:** New capability `agents-interviewer-workflow`; touches `agents-orchestrator-workflow` (Interviewer phase now has handler)
- **Affected code:** 
  - New: `nirmata.Agents/Execution/Planning/NewProjectInterviewer/**`
  - Modified: `nirmata.Agents/Execution/Orchestrator/` (wire up Interviewer dispatch)
- **Workspace outputs:** `.aos/spec/project.json`, `.aos/evidence/runs/RUN-*/artifacts/interview.*.md`

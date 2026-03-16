# Change: Add minimal planned task executor

## Why
Milestone E0 requires a deterministic “hello-world” loop that can execute a persisted plan and produce proof (evidence) without relying on chat history.

We already have workspace bootstrap, schema/workspace validation gates, and run evidence scaffolding. What’s missing is a minimal executor that can read a stored plan and generate deterministic outputs + an evidence trail.

## What Changes
- Add a new CLI command:
  - `aos execute-plan`
- Define a minimal persisted plan file contract for E0 “toy” execution.
- Implement a minimal executor that:
  - auto-starts a run (creates `.aos/evidence/runs/RUN-*/...`)
  - writes outputs only under `.aos/evidence/runs/<run-id>/outputs/**`
  - records an actions log under `.aos/evidence/runs/<run-id>/logs/**`
  - finishes the run on success
- Organize implementation for separation of concerns:
  - plan loading + execution under `nirmata.Aos/Engine/ExecutePlan/**`
  - evidence writing helpers under `nirmata.Aos/Engine/Evidence/ExecutePlan/**`

## Impact
- **Affected specs**:
  - new capability `aos-execute-plan`
- **Affected code** (expected during implementation):
  - `nirmata.Aos` CLI command routing (`aos execute-plan ...`)
  - plan loading + validation under `nirmata.Aos/Engine/ExecutePlan/**`
  - evidence writing code under `nirmata.Aos/Engine/Evidence/ExecutePlan/**`
  - runtime evidence artifacts under `.aos/evidence/runs/<run-id>/**`
  - tests proving deterministic outputs and evidence capture


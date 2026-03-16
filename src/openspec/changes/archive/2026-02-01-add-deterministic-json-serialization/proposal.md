# Change: add-deterministic-json-serialization

## Why
The engine emits JSON artifacts under `.aos/**` that are expected to be deterministic for fixture comparison and reproducible runs. Today we normalize line endings and use `System.Text.Json` indentation, but we do not guarantee canonical key ordering across hosts/runtimes and we do not write atomically, risking partial/corrupt artifacts.

## What Changes
- Introduce a new capability `aos-deterministic-json-serialization` that defines a single canonical deterministic JSON writer for AOS-emitted artifacts:
  - recursive ordinal key sorting (canonical JSON)
  - stable formatting + UTF-8 (no BOM) + LF + trailing newline
  - atomic write semantics (temp + replace/move)
  - no-churn semantics when canonical bytes are unchanged
- Update existing AOS specs that currently require “deterministic JSON” to explicitly depend on this canonical writer and to require atomic writes:
  - `aos-workspace-bootstrap`
  - `aos-run-lifecycle`
  - `aos-execute-plan`

## Impact
- **Affected specs**:
  - Added: `aos-deterministic-json-serialization`
  - Modified: `aos-workspace-bootstrap`, `aos-run-lifecycle`, `aos-execute-plan`
- **Affected code (expected implementation touchpoints)**:
  - `nirmata.Aos/Engine/Workspace/AosWorkspaceBootstrapper.cs` (init JSON templates + registry)
  - `nirmata.Aos/Engine/Evidence/Runs/AosRunEvidenceScaffolder.cs` (run.json + runs/index.json)
  - `nirmata.Aos/Engine/Evidence/ExecutePlan/ExecutePlanActionsLogWriter.cs` (execute-plan.actions.json)
  - New shared writer utility under `nirmata.Aos` engine (canonicalize + atomic/no-churn write)
- **Risks**:
  - Canonicalization adds overhead, but artifact sizes in this milestone are small and determinism is the priority.
  - Atomic writes must be carefully implemented per platform semantics; this proposal constrains behavior via an engine-owned abstraction.

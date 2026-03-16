# Change: Add paths and ID routing (AOS workspace)

## Why
Milestone E1 requires the AOS workspace to be enforceable and machine-operable without relying on “convention” or ad-hoc path guessing.

Today, path logic exists in narrow, isolated places (evidence runs, execute-plan outputs), but there is no single routing source of truth for spec/state/evidence artifacts. That makes new CLI commands and stores error-prone (duplicate rules, inconsistent paths, and drift).

## What Changes
- Define a canonical mapping from artifact IDs to deterministic `.aos/*` paths for the following kinds:
  - RUN (run evidence)
  - MS (milestone spec)
  - PH (phase spec)
  - TSK (task spec)
  - ISS (issue spec)
  - UAT (uat spec)
- Establish a single “router” concept that all engine/CLI code must use to resolve artifact paths (no ad-hoc `Path.Combine` with magic segments).
- Codify ID parsing/validation rules so the router can reject invalid IDs with actionable errors.

## Impact
- **Affected specs**:
  - new capability `aos-path-routing`
  - (likely follow-up) existing capabilities will reference the router when they add CRUD/stores/index repair
- **Affected code** (expected during implementation):
  - `nirmata.Aos` CLI command handlers that read/write `.aos/spec/**` artifacts
  - `nirmata.Aos/Engine/**` filesystem policies for spec/state/evidence routing
  - tests verifying routing is deterministic and unique per artifact ID


# Change: Add run lifecycle evidence scaffold

## Why
The engine needs a deterministic, standard evidence folder contract for “runs” so that execution can be observed, audited, and resumed without relying on chat history.

This milestone introduces the minimal run lifecycle surface area (start/finish) and the evidence structure that later workflows will write into.

## What Changes
- Add CLI commands:
  - `aos run start`
  - `aos run finish`
- Create a run evidence folder structure under `.aos/evidence/runs/**`.
- Implement run ID generation + routing.
- Write deterministic evidence capture scaffolding (logs, outputs, metadata) and a metadata index.

## Impact
- **Affected specs**:
  - new capability `aos-run-lifecycle`
- **Affected code** (expected during implementation):
  - `nirmata.Aos` CLI command routing (`aos run ...`)
  - evidence writing logic under `nirmata.Aos/Engine/Evidence/**` (writes into `.aos/evidence/runs/**`)
  - run evidence writers under `.aos/evidence/runs/**`
  - tests proving deterministic folder creation and index updates


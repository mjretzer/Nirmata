# aos-agent-io-contracts Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `nirmata.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: Specialist agent request/result artifacts use uniform shapes
The system SHALL define uniform request/result shapes for specialist agent invocations so orchestration can be audited and resumed without chat state.

For each agent invocation, the system MUST persist:
- a request document
- a result document

The request and result documents MUST be written as deterministic JSON using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The request/result documents MUST include at least:
- `schemaVersion` (integer)
- `runId` (string)
- `agentId` (string)
- `requestId` (string)

#### Scenario: A specialist agent invocation records request and result artifacts
- **GIVEN** an orchestration run that invokes a specialist agent
- **WHEN** the agent invocation completes (success or failure)
- **THEN** the request and result documents exist on disk and include the required fields

